using UnityEngine;

namespace ClashUp.Client.Gameplay
{
    public sealed class BillboardLabel : MonoBehaviour
    {
        private Camera _cam;

        private void LateUpdate()
        {
            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null) return;
            }

            transform.rotation = _cam.transform.rotation;
        }
    }
}
