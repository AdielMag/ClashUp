using nkast.Aether.Physics2D.Dynamics;
using UnityEngine;
using AetherNet.Collision;

namespace AetherNet
{
    [AddComponentMenu("AetherNet/Circle Collider")]
    public sealed class AetherCircleCollider : MonoBehaviour, IAetherColliderProvider
    {
        [SerializeField] private float                 _radius    = 0.5f;
        [SerializeField] private Vector2               _offset    = Vector2.zero;
        [SerializeField] private bool                  _isTrigger;
        [SerializeField] private int                   _layer;
        [SerializeField] private AetherPhysicsMaterial _material;

        public float   Radius    => _radius;
        public Vector2 Offset    => _offset;
        public bool    IsTrigger => _isTrigger;

        void IAetherColliderProvider.AttachToBody(Body body, PhysicsWorldManager world)
        {
            float density     = _material != null ? _material.Density     : 1f;
            float friction    = _material != null ? _material.Friction    : 0.2f;
            float restitution = _material != null ? _material.Restitution : 0f;

            float simRadius = _radius / SimulationConstants.PixelsPerMeter;
            var   simOffset = MathBridge.ToNumerics(_offset) / SimulationConstants.PixelsPerMeter;

            Fixture fixture = body.CreateCircle(simRadius, density, AetherInterop.ToAether(simOffset));
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
