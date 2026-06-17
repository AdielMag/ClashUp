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
        private float _cachedWidth;
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
                      || config.Width  != _cachedWidth
                      || !Mathf.Approximately(aimYaw, _cachedAimYaw);

            if (!dirty) return;

            _cachedShape   = config.Shape;
            _cachedRadius  = config.Radius;
            _cachedLength  = config.Length;
            _cachedAngle   = config.Angle;
            _cachedWidth   = config.Width;
            _cachedAimYaw  = aimYaw;

            AbilityShapeMesh.Build(_mesh, config, aimYaw);
        }
    }
}
