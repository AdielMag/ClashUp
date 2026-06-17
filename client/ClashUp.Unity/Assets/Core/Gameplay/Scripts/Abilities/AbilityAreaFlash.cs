using ClashUp.Shared.Abilities;
using UnityEngine;

namespace ClashUp.Client.Gameplay
{
    /// <summary>
    /// A brief ground flash in the exact damage footprint of an ability, spawned on cast.
    /// Builds its mesh from the server-derived <see cref="TelegraphConfig"/> footprint so the
    /// visual always matches what actually deals damage, then fades out and self-destroys.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class AbilityAreaFlash : MonoBehaviour
    {
        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private Material _mat;
        private Color _baseColor;
        private float _duration = 0.22f;
        private float _elapsed;

        public static AbilityAreaFlash Spawn(TelegraphConfig footprint, Vector3 origin, float aimYaw, Color color, float duration)
        {
            var go = new GameObject("AbilityAreaFlash");
            go.transform.position = origin;
            var flash = go.AddComponent<AbilityAreaFlash>();
            flash.Play(footprint, aimYaw, color, duration);
            return flash;
        }

        public void Play(TelegraphConfig footprint, float aimYaw, Color color, float duration)
        {
            EnsureComponents();
            _baseColor = color;
            _duration = Mathf.Max(0.01f, duration);
            _elapsed = 0f;
            _mat.color = color;
            AbilityShapeMesh.Build(_mesh, footprint, aimYaw);
        }

        private void EnsureComponents()
        {
            if (_mesh != null) return;
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _mesh = new Mesh { name = "AbilityAreaFlashMesh" };
            _filter.sharedMesh = _mesh;
            _mat = new Material(Shader.Find("Sprites/Default"));
            _renderer.sharedMaterial = _mat;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = _elapsed / _duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            var c = _baseColor;
            c.a = _baseColor.a * (1f - t);
            _mat.color = c;
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
