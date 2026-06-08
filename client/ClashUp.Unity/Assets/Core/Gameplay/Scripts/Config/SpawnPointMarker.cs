using UnityEngine;

namespace ClashUp.Client.Gameplay
{
    public sealed class SpawnPointMarker : MonoBehaviour
    {
        [SerializeField] private int _teamIndex;
        [SerializeField] private int _slotIndex;

        public int TeamIndex => _teamIndex;
        public int SlotIndex => _slotIndex;
    }
}
