using UnityEngine;

namespace ClashUp.Client.Gameplay
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRegistrant : MonoBehaviour
    {
        [SerializeField] private bool _isMatchCamera;

        public bool IsMatchCamera
        {
            get => _isMatchCamera;
            set => _isMatchCamera = value;
        }

        private Camera _camera;

        private void Awake() => _camera = GetComponent<Camera>();

        private void Start() => CameraService.Instance.Register(_camera, _isMatchCamera);

        private void OnDestroy() => CameraService.Instance.Unregister(_camera);
    }
}
