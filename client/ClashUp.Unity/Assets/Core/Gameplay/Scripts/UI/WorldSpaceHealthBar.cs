using UnityEngine;
using UnityEngine.UI;

namespace ClashUp.Client.Gameplay
{
    public sealed class WorldSpaceHealthBar : MonoBehaviour
    {
        [SerializeField] private Image _fill;

        public void SetHealth(float current, float max)
        {
            if (_fill == null || max <= 0f) return;
            _fill.fillAmount = Mathf.Clamp01(current / max);
        }
    }
}
