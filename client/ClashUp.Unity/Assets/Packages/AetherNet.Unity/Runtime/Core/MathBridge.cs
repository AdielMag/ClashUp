using System.Runtime.CompilerServices;

namespace AetherNet
{
    /// <summary>
    /// Zero-cost conversions between UnityEngine.Vector2/3 and System.Numerics.Vector2.
    /// Both types are sequential float x, y — the unsafe reinterpret cast is bitwise safe.
    /// </summary>
    public static unsafe class MathBridge
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static System.Numerics.Vector2 ToNumerics(UnityEngine.Vector2 v)
            => *(System.Numerics.Vector2*)&v;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnityEngine.Vector2 ToUnity(System.Numerics.Vector2 v)
            => *(UnityEngine.Vector2*)&v;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnityEngine.Vector3 ToUnity3(System.Numerics.Vector2 v)
            => SimulationConstants.Plane == SimulationPlane.XZ
                ? new UnityEngine.Vector3(v.X, 0f, v.Y)
                : new UnityEngine.Vector3(v.X, v.Y, 0f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static System.Numerics.Vector2 WorldToSim(UnityEngine.Vector2 worldPos)
        {
            var n = ToNumerics(worldPos);
            return MathExtensions.ToSimulation(in n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static System.Numerics.Vector2 WorldToSim(UnityEngine.Vector3 worldPos)
        {
            var n = SimulationConstants.Plane == SimulationPlane.XZ
                ? new System.Numerics.Vector2(worldPos.x, worldPos.z)
                : new System.Numerics.Vector2(worldPos.x, worldPos.y);
            return MathExtensions.ToSimulation(in n);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnityEngine.Vector2 SimToWorld(System.Numerics.Vector2 simPos)
            => ToUnity(MathExtensions.ToWorld(in simPos));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnityEngine.Vector3 SimToWorld3(System.Numerics.Vector2 simPos)
            => ToUnity3(MathExtensions.ToWorld(in simPos));
    }
}
