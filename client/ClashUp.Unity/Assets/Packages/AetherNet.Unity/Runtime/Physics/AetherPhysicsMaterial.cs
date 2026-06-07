using UnityEngine;

namespace AetherNet
{
    [CreateAssetMenu(fileName = "AetherPhysicsMaterial", menuName = "AetherNet/Physics Material")]
    public sealed class AetherPhysicsMaterial : ScriptableObject
    {
        [Range(0f, 10f)] public float Friction    = 0.2f;
        [Range(0f, 1f)]  public float Restitution = 0f;
        [Min(0.001f)]    public float Density     = 1f;
    }
}
