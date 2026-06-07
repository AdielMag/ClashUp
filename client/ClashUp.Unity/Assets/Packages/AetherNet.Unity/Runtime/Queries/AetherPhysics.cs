#nullable enable
using UnityEngine;
using AetherNet.Queries;
using RaycastHit = AetherNet.Queries.RaycastHit;

namespace AetherNet
{
    /// <summary>
    /// Static query API mirroring Unity's Physics2D interface.
    /// Backed by a shared PhysicsQueryBuffer allocated once by AetherViewManager.
    /// </summary>
    public static class AetherPhysicsQueries
    {
        private static PhysicsWorldManager? _world;
        private static PhysicsQueryBuffer?  _buffer;

        internal static void Initialize(PhysicsWorldManager world, PhysicsQueryBuffer buffer)
        {
            _world  = world;
            _buffer = buffer;
        }

        public static int Raycast(
            Vector2      origin,
            Vector2      direction,
            float        distance,
            RaycastHit[] results,
            int          layerMask = -1)
        {
            if (_world == null || _buffer == null) return 0;
            var simOrigin = MathBridge.WorldToSim(origin);
            var simDir    = MathBridge.ToNumerics(direction.normalized);
            float simDist = distance / SimulationConstants.PixelsPerMeter;
            _world.Raycast(in simOrigin, in simDir, simDist, _buffer, layerMask);
            int count = _buffer.RaycastCount < results.Length ? _buffer.RaycastCount : results.Length;
            for (int i = 0; i < count; i++) results[i] = _buffer.RaycastResults[i];
            return count;
        }

        public static bool Raycast(Vector2 origin, Vector2 direction, float distance,
                                   out RaycastHit hit, int layerMask = -1)
        {
            var results = new RaycastHit[1];
            int count   = Raycast(origin, direction, distance, results, layerMask);
            hit = count > 0 ? results[0] : default;
            return count > 0;
        }
    }
}
