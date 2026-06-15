using System;

namespace ClashUp.Client.Gameplay
{
    public interface IAbilityInput
    {
        uint ButtonMask { get; }
        float AimYaw { get; }
        float LiveAimYaw { get; }
        event Action<bool> OnTouching;
        void Poll();
        void ConsumeInput();
    }
}
