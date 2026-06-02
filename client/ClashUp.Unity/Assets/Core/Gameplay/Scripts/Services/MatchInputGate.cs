using System;

namespace ClashUp.Client.Gameplay
{
    public sealed class MatchInputGate
    {
        public bool IsEnabled { get; private set; }

        public event Action<bool> OnChanged;

        public void Enable()
        {
            IsEnabled = true;
            OnChanged?.Invoke(true);
        }

        public void Disable()
        {
            IsEnabled = false;
            OnChanged?.Invoke(false);
        }
    }
}
