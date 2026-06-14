namespace ClashUp.Client.Gameplay
{
    public interface IAbilityInput
    {
        uint ButtonMask { get; }
        float AimYaw { get; }
        void Poll();
        void ConsumeInput();
    }
}
