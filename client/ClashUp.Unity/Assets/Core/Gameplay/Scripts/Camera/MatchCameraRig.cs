using System;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;
using VContainer.Unity;

namespace ClashUp.Client.Gameplay
{
    public sealed class MatchCameraRig : IStartable, IDisposable
    {
        private static readonly Vector3 FollowOffset = new Vector3(0f, 32f, -24f);

        private readonly PlayerSpawner _playerSpawner;
        private GameObject _cameraGo;
        private GameObject _vcamGo;

        public MatchCameraRig(PlayerSpawner playerSpawner)
        {
            _playerSpawner = playerSpawner;
        }

        public void Start()
        {
            _cameraGo = BuildMainCamera();
            _vcamGo = BuildVirtualCamera(_playerSpawner.PlayerTransform);
        }

        public void Dispose()
        {
            if (_vcamGo != null) UnityEngine.Object.Destroy(_vcamGo);
            if (_cameraGo != null) UnityEngine.Object.Destroy(_cameraGo);
        }

        private static GameObject BuildMainCamera()
        {
            var go = new GameObject("MatchCamera");
            go.AddComponent<Camera>();
            go.AddComponent<CinemachineBrain>();
            return go;
        }

        private static GameObject BuildVirtualCamera(Transform target)
        {
            var go = new GameObject("MatchVirtualCamera");

            // Fixed rotation: always look from offset position toward world origin below
            go.transform.rotation = Quaternion.LookRotation(-FollowOffset);

            var vcam = go.AddComponent<CinemachineCamera>();
            vcam.Follow = target;

            var follow = go.AddComponent<CinemachineFollow>();
            follow.FollowOffset = FollowOffset;

            // World-space offset, light damping for smooth but tight follow
            var settings = follow.TrackerSettings;
            settings.BindingMode = BindingMode.WorldSpace;
            settings.PositionDamping = new Vector3(0.15f, 0.15f, 0.15f);
            follow.TrackerSettings = settings;

            return go;
        }
    }
}
