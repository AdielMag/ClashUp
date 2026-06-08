using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Client.Gameplay
{
    public sealed class PlayerRenderState
    {
        public PlayerId Id;

        // Latest predicted tick state.
        public float X;
        public float Z;
        public float Yaw;

        // Previous predicted tick state. The view lerps Prev -> current by the
        // sub-tick alpha so the local player renders smoothly between fixed steps.
        public float PrevX;
        public float PrevZ;
        public float PrevYaw;

        public float Health;
        public float MaxHealth;
    }
}
