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
        private readonly IAbilityInput _abilityInput;
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
            IAbilityInput abilityInput,
            MatchInputGate gate,
            ClientPredictionWorld prediction,
            MatchSession session)
        {
            _input = input;
            _abilityInput = abilityInput;
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

            _prediction.ProcessPendingSnapshots();

            _abilityInput.Poll();

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
            uint buttonMask = _abilityInput.ButtonMask;
            float aimYaw = _abilityInput.AimYaw;

            var cmd = new InputCommand
            {
                Tick = _tick,
                ClientSendStampMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                MoveX = MovementModel.EncodeAxis(dir.x),
                MoveY = MovementModel.EncodeAxis(dir.y),
                ButtonMask = buttonMask,
                AimYawQ = MovementModel.EncodeAxis(aimYaw / 180f),
                SequenceId = _sequenceId,
            };

            _abilityInput.ConsumeInput();

            _prediction.Predict(cmd);
            _session.SubmitInputAsync(cmd).Forget();
        }
    }
}
