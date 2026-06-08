using System;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;
using VContainer.Unity;

namespace ClashUp.Client.Gameplay
{
    public sealed class MatchCameraRig : IStartable, ITickable, IDisposable
    {
        private static readonly Vector3 FollowOffset = new Vector3(0f, 32f, -24f);

        private readonly PlayerViewSystem _viewSystem;
        private GameObject _cameraGo;
        private GameObject _vcamGo;
        private CinemachineCamera _vcam;
        private bool _followAssigned;

        public MatchCameraRig(PlayerViewSystem viewSystem)
        {
            _viewSystem = viewSystem;
        }

        public void Start()
        {
            _cameraGo = BuildMainCamera();
            _vcamGo = BuildVirtualCamera();
        }

        public void Tick()
        {
            if (_followAssigned) return;
            var target = _viewSystem.LocalPlayerTransform;
            if (target == null) return;

            _vcam.Follow = target;
            _followAssigned = true;
        }

        public void Dispose()
        {
            if (_vcamGo != null) UnityEngine.Object.Destroy(_vcamGo);
            if (_cameraGo != null) UnityEngine.Object.Destroy(_cameraGo);
        }

        private static GameObject BuildMainCamera()
        {
            var go = new GameObject("MatchCamera");
            go.tag = "MainCamera";
            go.AddComponent<Camera>();
            go.AddComponent<CinemachineBrain>();
            return go;
        }

        private GameObject BuildVirtualCamera()
        {
            var go = new GameObject("MatchVirtualCamera");
            go.transform.rotation = Quaternion.LookRotation(-FollowOffset);

            _vcam = go.AddComponent<CinemachineCamera>();

            var follow = go.AddComponent<CinemachineFollow>();
            follow.FollowOffset = FollowOffset;

            var settings = follow.TrackerSettings;
            settings.BindingMode = BindingMode.WorldSpace;
            settings.PositionDamping = new Vector3(0.15f, 0.15f, 0.15f);
            follow.TrackerSettings = settings;

            return go;
        }
    }
}
