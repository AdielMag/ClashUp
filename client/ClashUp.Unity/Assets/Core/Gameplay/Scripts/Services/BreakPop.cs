using UnityEngine;

namespace ClashUp.Client.Gameplay
{
    /// <summary>
    /// Cheap, self-destroying "pop" used when a breakable box shatters. A small sphere that scales up
    /// and fades over a fraction of a second. Code-driven so no prefab wiring is required.
    /// </summary>
    public sealed class BreakPop : MonoBehaviour
    {
        private const float Duration = 0.35f;
        private float _age;
        private Material _material;
        private Color _baseColor = new(0.9f, 0.7f, 0.25f, 1f);

        public static void Spawn(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "BoxBreakPop";
            go.transform.position = position;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.AddComponent<BreakPop>();
        }

        private void Awake()
        {
            var r = GetComponent<Renderer>();
            _material = r != null ? r.material : null;
            if (_material != null) _material.color = _baseColor;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = _age / Duration;
            if (t >= 1f) { Destroy(gameObject); return; }

            transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.6f, t);
            if (_material != null)
            {
                var c = _baseColor;
                c.a = 1f - t;
                _material.color = c;
            }
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
