using UnityEngine;

namespace AetherNet.Editor
{
    internal static class GizmoHelper
    {
        /// <summary>Maps a 2D offset to the correct 3D axes based on SimulationPlane.</summary>
        internal static Vector3 To3D(Vector2 v)
            => SimulationConstants.Plane == SimulationPlane.XZ
                ? new Vector3(v.x, 0f, v.y)
                : new Vector3(v.x, v.y, 0f);

        /// <summary>Returns the normal vector for the simulation plane disc/wireframe drawing.</summary>
        internal static Vector3 PlaneNormal
            => SimulationConstants.Plane == SimulationPlane.XZ
                ? Vector3.up
                : Vector3.forward;
    }
}
