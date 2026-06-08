using ClashUp.Client.Gameplay;
using ClashUp.Client.Networking;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Simulation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace ClashUp.Client.Match
{
    public sealed class LocalInputPublisher : ITickable
    {
        private readonly IMovementInput _input;
        private readonly MatchInputGate _gate;
        private readonly ClientPredictionWorld _prediction;
        private readonly MatchSession _session;

        private int _tick;
        private int _sequenceId;
        private float _tickInterval;
        private float _accumulator;
        private bool _started;

        public LocalInputPublisher(
            IMovementInput input,
            MatchInputGate gate,
            ClientPredictionWorld prediction,
            MatchSession session)
        {
            _input = input;
            _gate = gate;
            _prediction = prediction;
            _session = session;
        }

        public void Configure(int startTick, int tickRateHz)
        {
            _tick = startTick;
            _tickInterval = 1f / tickRateHz;
            _started = true;
        }

        public void Tick()
        {
            if (!_started || !_gate.IsEnabled) return;

            // Process any snapshots that arrived since last frame BEFORE accumulating
            // time or computing alpha. This ensures PrevX/X and RenderAlpha are always
            // consistent — no mid-frame corruption from MagicOnion callback timing.
            _prediction.ProcessPendingSnapshots();

            _accumulator += Time.deltaTime;
            while (_accumulator >= _tickInterval)
            {
                _accumulator -= _tickInterval;
                SendTick();
            }

            _prediction.RenderAlpha = _tickInterval > 0f ? Mathf.Clamp01(_accumulator / _tickInterval) : 0f;
        }

        private void SendTick()
        {
            _tick++;
            _sequenceId++;

            var dir = _input.Value;
            var cmd = new InputCommand
            {
                Tick = _tick,
                ClientSendStampMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                MoveX = MovementModel.EncodeAxis(dir.x),
                MoveY = MovementModel.EncodeAxis(dir.y),
                SequenceId = _sequenceId,
            };

            _prediction.Predict(cmd);
            _session.SubmitInputAsync(cmd).Forget();
        }
    }
}
