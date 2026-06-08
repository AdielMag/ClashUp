using System;
using System.Collections.Generic;
using ClashUp.Shared.MessagePackObjects;
using MessagePack;

namespace ClashUp.Client.Gameplay
{
    /// <summary>
    /// Client-side prediction + server reconciliation (Gambetta).
    /// The local player is simulated immediately on input; when an authoritative snapshot
    /// arrives, the local state is reset to the server's, acked inputs are dropped by
    /// sequence id, and the still-pending inputs are replayed forward. Remote players are
    /// peeled off the snapshot and handed to <see cref="RemotePlayerInterpolator"/>.
    ///
    /// Snapshots are queued on arrival (MagicOnion callback timing is non-deterministic)
    /// and processed at the start of <see cref="ProcessPendingSnapshots"/> — called by
    /// the input publisher before accumulating time — so that PrevX/X and RenderAlpha
    /// are always consistent when the view reads them.
    /// </summary>
    public sealed class ClientPredictionWorld
    {
        private readonly IClientSimulation _sim;
        private readonly RemotePlayerInterpolator _interpolator;
        private readonly Queue<InputCommand> _pendingInputs = new();
        private readonly Queue<SnapshotPacket> _snapshotQueue = new();
        private double _tickInterval;

        // Reconciliation correction smoothing: absorbs small prediction errors over several
        // frames instead of hard-popping. Exponentially decayed toward zero each frame.
        private const float CorrectionDecayRate = 18f; // per second; ~95% corrected in ~160ms

        public ClientPredictionWorld(IClientSimulation sim, RemotePlayerInterpolator interpolator)
        {
            _sim = sim;
            _interpolator = interpolator;
        }

        public int CurrentTick => _sim.CurrentTick;

        /// <summary>
        /// Sub-tick interpolation fraction (0..1) of the current prediction step, written by
        /// the input loop each frame and read by the view to smooth the local player. Lives
        /// here (Gameplay) so the view need not reference the Match assembly.
        /// </summary>
        public float RenderAlpha { get; set; }

        /// <summary>Residual visual offset from reconciliation corrections, decayed each frame.</summary>
        public float CorrectionX { get; private set; }
        public float CorrectionZ { get; private set; }

        public void Configure(int tickRateHz)
        {
            _tickInterval = 1.0 / tickRateHz;
            _interpolator.Configure(tickRateHz);
        }

        public void SetRandomSeed(uint seed) => _sim.SetRandomSeed(seed);

        public void Predict(InputCommand command)
        {
            _sim.ApplyLocalInput(command);
            _sim.Step(_tickInterval);
            _pendingInputs.Enqueue(command);
        }

        /// <summary>
        /// Enqueue a snapshot for deferred processing. Called from the MagicOnion callback,
        /// which can fire at any point in the frame. The actual reconciliation happens in
        /// <see cref="ProcessPendingSnapshots"/>.
        /// </summary>
        public void EnqueueSnapshot(SnapshotPacket snapshot) => _snapshotQueue.Enqueue(snapshot);

        /// <summary>
        /// Process all queued snapshots. Must be called at a deterministic point in the frame
        /// — before accumulating time and computing RenderAlpha — so that the render state
        /// (PrevX/X) and alpha are always consistent when the view reads them.
        /// </summary>
        public void ProcessPendingSnapshots()
        {
            while (_snapshotQueue.Count > 0)
                ApplyServerSnapshot(_snapshotQueue.Dequeue());
        }

        private void ApplyServerSnapshot(SnapshotPacket snapshot)
        {
            WorldStatePacket packet = snapshot.DeltaBlob.Length > 0
                ? MessagePackSerializer.Deserialize<WorldStatePacket>(snapshot.DeltaBlob)
                : null;

            // Buffer remote players for entity interpolation.
            if (packet != null)
            {
                var localId = _sim.LocalId;
                foreach (var dto in packet.Players)
                {
                    if (dto.Id.Equals(localId)) continue;
                    _interpolator.AddSample(dto.Id.Value, snapshot.ServerStampMs, dto.X, dto.Z, dto.Yaw, dto.Health);
                }
            }

            // Capture the pre-reconcile PHYSICS position for correction smoothing.
            // Using physics (not render state) avoids contaminating the correction with
            // render-state artifacts that cause backward drift during decay.
            _sim.TryGetPhysicsPosition(out float prePhysX, out float prePhysZ);

            // Reset local player to authoritative state and learn the last input the server acked.
            int ackedSeq = _sim.ReconcileTo(snapshot.Tick, packet);

            // Drop inputs the server has already processed.
            while (_pendingInputs.Count > 0 && _pendingInputs.Peek().SequenceId <= ackedSeq)
                _pendingInputs.Dequeue();

            // Replay the inputs still in flight on top of the authoritative state.
            // Uses StepPhysicsOnly so that PrevX/X (render interpolation targets)
            // are NOT modified — only normal Predict→Step updates them.
            foreach (var queued in _pendingInputs)
            {
                _sim.ApplyLocalInput(queued);
                _sim.StepPhysicsOnly(_tickInterval);
            }

            // Accumulate the visual correction offset from the physics-level delta.
            if (_sim.TryGetPhysicsPosition(out float postPhysX, out float postPhysZ))
            {
                CorrectionX += prePhysX - postPhysX;
                CorrectionZ += prePhysZ - postPhysZ;
            }
        }

        /// <summary>Decay the correction offset. Called once per frame by the view before rendering.</summary>
        public void DecayCorrection(float deltaTime)
        {
            float factor = MathF.Exp(-CorrectionDecayRate * deltaTime);
            CorrectionX *= factor;
            CorrectionZ *= factor;
            if (CorrectionX * CorrectionX + CorrectionZ * CorrectionZ < 1e-8f)
            {
                CorrectionX = 0f;
                CorrectionZ = 0f;
            }
        }
    }
}
