using ClashUp.Shared.Abilities;
using UnityEngine;

namespace ClashUp.Client.Gameplay
{
    /// <summary>
    /// Builds flat XZ-plane meshes for ability shapes (circle, forward line, cone, capsule).
    /// Shared by the persistent telegraph (TelegraphRenderer) and the triggered area flash
    /// (AbilityAreaFlash) so both render identical footprints. All meshes are local-space:
    /// place the owning transform at the caster and the shape extends along aimYaw.
    /// </summary>
    public static class AbilityShapeMesh
    {
        private const float DefaultLineWidth = 0.3f;

        public static void Build(Mesh mesh, TelegraphConfig config, float aimYaw)
        {
            switch (config.Shape)
            {
                case TelegraphShape.CircleAroundCaster:
                case TelegraphShape.TargetCircle:
                    BuildCircle(mesh, config.Radius);
                    break;
                case TelegraphShape.ForwardLine:
                    BuildForwardLine(mesh, config.Length, config.Width > 0f ? config.Width : DefaultLineWidth, aimYaw);
                    break;
                case TelegraphShape.ForwardCone:
                    BuildCone(mesh, config.Length, config.Angle, aimYaw);
                    break;
                case TelegraphShape.Capsule:
                    BuildCapsule(mesh, config.Length, config.Width, aimYaw);
                    break;
            }
        }

        public static void BuildCircle(Mesh mesh, float radius, int segments = 32)
        {
            var verts = new Vector3[segments + 1];
            var tris = new int[segments * 3];

            verts[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                verts[i + 1] = new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
            }

            for (int i = 0; i < segments; i++)
            {
                tris[i * 3 + 0] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = (i + 1) % segments + 1;
            }

            Assign(mesh, verts, tris);
        }

        public static void BuildForwardLine(Mesh mesh, float length, float width, float aimYaw)
        {
            float halfW = width * 0.5f;
            float yawRad = aimYaw * Mathf.Deg2Rad;
            float dx = Mathf.Sin(yawRad);
            float dz = Mathf.Cos(yawRad);
            float px = -dz * halfW;
            float pz = dx * halfW;

            var verts = new[]
            {
                new Vector3(-px, 0f, -pz),
                new Vector3( px, 0f,  pz),
                new Vector3(dx * length + px, 0f, dz * length + pz),
                new Vector3(dx * length - px, 0f, dz * length - pz),
            };
            Assign(mesh, verts, new[] { 0, 1, 2, 0, 2, 3 });
        }

        public static void BuildCone(Mesh mesh, float length, float angleDeg, float aimYaw)
        {
            int segments = 16;
            float halfAngle = angleDeg * 0.5f * Mathf.Deg2Rad;
            float yawRad = aimYaw * Mathf.Deg2Rad;

            var verts = new Vector3[segments + 2];
            var tris = new int[segments * 3];

            verts[0] = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float a = yawRad - halfAngle + t * 2f * halfAngle;
                verts[i + 1] = new Vector3(Mathf.Sin(a) * length, 0f, Mathf.Cos(a) * length);
            }

            for (int i = 0; i < segments; i++)
            {
                tris[i * 3 + 0] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }

            Assign(mesh, verts, tris);
        }

        // Stadium / capsule: a forward rectangle (half-width width/2) with a rounded cap at
        // each end. Built as a triangle fan from the segment midpoint around the perimeter.
        public static void BuildCapsule(Mesh mesh, float length, float width, float aimYaw)
        {
            float hw = width * 0.5f;
            float yaw = aimYaw * Mathf.Deg2Rad;
            float dx = Mathf.Sin(yaw);
            float dz = Mathf.Cos(yaw);
            float ex = dx * length;
            float ez = dz * length;

            const int capSegs = 12;
            int perim = (capSegs + 1) * 2;
            var verts = new Vector3[perim + 1];
            var tris = new int[perim * 3];

            verts[0] = new Vector3(ex * 0.5f, 0f, ez * 0.5f); // centroid (segment midpoint)

            int idx = 1;
            // Far cap centered on the end point: phi sweeps the forward semicircle.
            for (int i = 0; i <= capSegs; i++)
            {
                float phi = yaw - Mathf.PI * 0.5f + Mathf.PI * i / capSegs;
                verts[idx++] = new Vector3(ex + Mathf.Sin(phi) * hw, 0f, ez + Mathf.Cos(phi) * hw);
            }
            // Near cap centered on the origin: phi continues sweeping the back semicircle.
            for (int i = 0; i <= capSegs; i++)
            {
                float phi = yaw + Mathf.PI * 0.5f + Mathf.PI * i / capSegs;
                verts[idx++] = new Vector3(Mathf.Sin(phi) * hw, 0f, Mathf.Cos(phi) * hw);
            }

            for (int i = 0; i < perim; i++)
            {
                tris[i * 3 + 0] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = (i + 1) % perim + 1;
            }

            Assign(mesh, verts, tris);
        }

        private static void Assign(Mesh mesh, Vector3[] verts, int[] tris)
        {
            mesh.Clear();
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
        }
    }
}
