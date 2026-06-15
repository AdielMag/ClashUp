using ClashUp.Shared.Abilities;
using UnityEngine;

namespace ClashUp.Client.Gameplay
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TelegraphRenderer : MonoBehaviour
    {
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Material _fallbackMat;

        // Cache to avoid rebuilding identical meshes every frame
        private TelegraphShape _cachedShape;
        private float _cachedRadius;
        private float _cachedLength;
        private float _cachedAngle;
        private float _cachedAimYaw = float.NaN;

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _mesh = new Mesh { name = "TelegraphMesh" };
            _meshFilter.sharedMesh = _mesh;
            _fallbackMat = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 1f, 0.4f, 0.3f) };
            _meshRenderer.sharedMaterial = _fallbackMat;
        }

        private void OnDestroy()
        {
            if (_fallbackMat != null)
                Destroy(_fallbackMat);
        }

        public void Show(TelegraphConfig config, TelegraphVisualData visual, Vector3 origin, float aimYaw)
        {
            if (visual?.TelegraphMaterial != null)
            {
                _meshRenderer.sharedMaterial = visual.TelegraphMaterial;
            }
            else
            {
                _fallbackMat.color = visual?.TelegraphColor ?? new Color(1f, 1f, 0.4f, 0.3f);
                _meshRenderer.sharedMaterial = _fallbackMat;
            }

            transform.position = origin;
            RebuildIfChanged(config, aimYaw);
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private void RebuildIfChanged(TelegraphConfig config, float aimYaw)
        {
            bool dirty = config.Shape  != _cachedShape
                      || config.Radius != _cachedRadius
                      || config.Length != _cachedLength
                      || config.Angle  != _cachedAngle
                      || !Mathf.Approximately(aimYaw, _cachedAimYaw);

            if (!dirty) return;

            _cachedShape   = config.Shape;
            _cachedRadius  = config.Radius;
            _cachedLength  = config.Length;
            _cachedAngle   = config.Angle;
            _cachedAimYaw  = aimYaw;

            _mesh.Clear();
            switch (config.Shape)
            {
                case TelegraphShape.CircleAroundCaster:
                    BuildCircle(config.Radius);
                    break;
                case TelegraphShape.ForwardLine:
                    BuildForwardLine(config.Length, 0.3f, aimYaw);
                    break;
                case TelegraphShape.ForwardCone:
                    BuildCone(config.Length, config.Angle, aimYaw);
                    break;
                case TelegraphShape.TargetCircle:
                    BuildCircle(config.Radius);
                    break;
            }
        }

        private void BuildCircle(float radius, int segments = 32)
        {
            var verts = new Vector3[segments + 1];
            var tris  = new int[segments * 3];

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

            _mesh.vertices  = verts;
            _mesh.triangles = tris;
            _mesh.RecalculateNormals();
        }

        private void BuildForwardLine(float length, float width, float aimYaw)
        {
            float halfW  = width * 0.5f;
            float yawRad = aimYaw * Mathf.Deg2Rad;
            float dx = Mathf.Sin(yawRad);
            float dz = Mathf.Cos(yawRad);
            float px = -dz * halfW;
            float pz =  dx * halfW;

            _mesh.vertices = new[]
            {
                new Vector3(-px, 0f, -pz),
                new Vector3( px, 0f,  pz),
                new Vector3(dx * length + px, 0f, dz * length + pz),
                new Vector3(dx * length - px, 0f, dz * length - pz),
            };
            _mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _mesh.RecalculateNormals();
        }

        private void BuildCone(float length, float angleDeg, float aimYaw)
        {
            int segments   = 16;
            float halfAngle = angleDeg * 0.5f * Mathf.Deg2Rad;
            float yawRad    = aimYaw * Mathf.Deg2Rad;

            var verts = new Vector3[segments + 2];
            var tris  = new int[segments * 3];

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

            _mesh.vertices  = verts;
            _mesh.triangles = tris;
            _mesh.RecalculateNormals();
        }
    }
}
