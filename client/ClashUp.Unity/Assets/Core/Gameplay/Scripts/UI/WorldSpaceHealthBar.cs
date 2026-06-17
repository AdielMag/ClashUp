using UnityEngine;
using UnityEngine.UI;

namespace ClashUp.Client.Gameplay
{
    public sealed class WorldSpaceHealthBar : MonoBehaviour
    {
        [SerializeField] private Slider _slider;

        public void SetHealth(float current, float max)
        {
            if (_slider == null || max <= 0f) return;
            _slider.value = Mathf.Clamp01(current / max);
        }
    }
}
