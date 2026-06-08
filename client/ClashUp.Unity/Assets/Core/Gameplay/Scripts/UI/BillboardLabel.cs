using UnityEngine;

namespace ClashUp.Client.Gameplay
{
    public sealed class BillboardLabel : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = CameraService.Instance.ActiveCamera;
            if (cam == null) return;

            var fwd = cam.transform.forward;
            var up = cam.transform.up;
            transform.rotation = Quaternion.LookRotation(fwd, up);
        }
    }
}
