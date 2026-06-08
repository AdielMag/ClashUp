using System.Collections.Generic;
using UnityEngine;

namespace ClashUp.Client.Gameplay
{
    public sealed class CameraService : ICameraService
    {
        private static CameraService _instance;
        public static ICameraService Instance => _instance ??= new CameraService();

        private readonly List<Camera> _cameras = new();
        private Camera _matchCamera;

        public Camera ActiveCamera => _matchCamera != null ? _matchCamera : Camera.main;

        private CameraService() { }

        public void Register(Camera camera, bool isMatchCamera = false)
        {
            if (camera == null) return;
            if (!_cameras.Contains(camera))
                _cameras.Add(camera);

            if (isMatchCamera)
            {
                _matchCamera = camera;
                foreach (var c in _cameras)
                    if (c != null && c != _matchCamera)
                        c.enabled = false;
            }
        }

        public void Unregister(Camera camera)
        {
            if (camera == null) return;
            _cameras.Remove(camera);

            if (_matchCamera == camera)
            {
                _matchCamera = null;
                foreach (var c in _cameras)
                    if (c != null)
                        c.enabled = true;
            }
        }
    }
}
