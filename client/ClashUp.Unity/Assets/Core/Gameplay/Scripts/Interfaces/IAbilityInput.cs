using System;

namespace ClashUp.Client.Gameplay
{
    public interface IAbilityInput
    {
        uint ButtonMask { get; }
        float AimYaw { get; }
        float LiveAimYaw { get; }

        // 0..1 joystick distance-from-center. Committed (for the fired cast) + live (telegraph preview).
        float AimMagnitude { get; }
        float LiveAimMagnitude { get; }
        event Action<bool> OnTouching;
        void Poll();
        void ConsumeInput();
    }
}
