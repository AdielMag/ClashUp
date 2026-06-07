using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Dynamics;
using UnityEngine;
using AetherNet.Collision;
using Vector2 = UnityEngine.Vector2;

namespace AetherNet
{
    [AddComponentMenu("AetherNet/Polygon Collider")]
    public sealed class AetherPolygonCollider : MonoBehaviour, IAetherColliderProvider
    {
        [SerializeField] private Vector2[]             _vertices  = System.Array.Empty<Vector2>();
        [SerializeField] private bool                  _isTrigger;
        [SerializeField] private int                   _layer;
        [SerializeField] private AetherPhysicsMaterial _material;

        private Vertices _simVertices;

        private void Awake()
        {
            _simVertices = new Vertices(_vertices.Length);
            for (int i = 0; i < _vertices.Length; i++)
            {
                var v = MathBridge.ToNumerics(_vertices[i]) / SimulationConstants.PixelsPerMeter;
                _simVertices.Add(AetherInterop.ToAether(v));
            }
        }

        void IAetherColliderProvider.AttachToBody(Body body, PhysicsWorldManager world)
        {
            if (_simVertices == null || _simVertices.Count < 3)
            {
                Debug.LogWarning($"[AetherNet] AetherPolygonCollider on '{name}' has fewer than 3 vertices — skipped.");
                return;
            }

            float density     = _material != null ? _material.Density     : 1f;
            float friction    = _material != null ? _material.Friction    : 0.2f;
            float restitution = _material != null ? _material.Restitution : 0f;

            Fixture fixture = body.CreatePolygon(_simVertices, density);
            fixture.Friction    = friction;
            fixture.Restitution = restitution;
            fixture.IsSensor    = _isTrigger;

            var filter = CollisionFilter.FromLayer(_layer);
            fixture.CollisionCategories = (Category)filter.CategoryBits;
            fixture.CollidesWith        = (Category)filter.MaskBits;
            fixture.CollisionGroup      = filter.GroupIndex;

            world.SubscribeFixtureEvents(fixture);
        }
    }
}
