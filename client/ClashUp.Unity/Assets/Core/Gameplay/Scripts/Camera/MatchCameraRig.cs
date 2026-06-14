using Unity.Cinemachine;
using UnityEngine;
using VContainer.Unity;

namespace ClashUp.Client.Gameplay
{
    public sealed class MatchCameraRig : ITickable
    {
        private readonly PlayerViewSystem _viewSystem;
        private readonly CinemachineCamera _vcam;
        private bool _followAssigned;

        public MatchCameraRig(PlayerViewSystem viewSystem, CinemachineCamera vcam)
        {
            _viewSystem = viewSystem;
            _vcam = vcam;
        }

        public void Tick()
        {
            if (_followAssigned) return;
            var target = _viewSystem.LocalPlayerTransform;
            if (target == null) return;

            _vcam.Follow = target;
            _followAssigned = true;
        }
    }
}
