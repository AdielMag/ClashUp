using UnityEngine;

namespace ClashUp.Client.Gameplay
{
    public interface ICameraService
    {
        Camera ActiveCamera { get; }
        void Register(Camera camera, bool isMatchCamera = false);
        void Unregister(Camera camera);
    }
}
