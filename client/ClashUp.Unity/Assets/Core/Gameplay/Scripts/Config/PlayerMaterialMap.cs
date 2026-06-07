using UnityEngine;

namespace ClashUp.Client.Gameplay
{
    [CreateAssetMenu(fileName = "PlayerMaterialMap", menuName = "ClashUp/Player Material Map")]
    public sealed class PlayerMaterialMap : ScriptableObject
    {
        [SerializeField] private Material[] _materials;

        public Material Get(int colorSlot)
        {
            return _materials[colorSlot % _materials.Length];
        }
    }
}
