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

            SyncAudioListeners();
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

            SyncAudioListeners();
        }

        // Guarantees exactly one enabled AudioListener across all loaded scenes.
        // Disabling a Camera leaves its sibling AudioListener active, so additive
        // scenes (CoreStarter + Matchmaking/Match) otherwise stack up listeners and
        // Unity warns "There are 2 audio listeners in the scene".
        private void SyncAudioListeners()
        {
            var all = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            var active = ActiveCamera;
            AudioListener chosen = active != null ? active.GetComponent<AudioListener>() : null;

            // Active camera has no listener of its own — keep one existing listener
            // alive rather than going silent (and only add one as a last resort).
            if (chosen == null)
            {
                foreach (var l in all)
                {
                    if (l == null) continue;
                    chosen = l;
                    break;
                }

                if (chosen == null && active != null)
                    chosen = active.gameObject.AddComponent<AudioListener>();
            }

            foreach (var l in all)
                if (l != null && l != chosen)
                    l.enabled = false;

            if (chosen != null)
                chosen.enabled = true;
        }
    }
}
