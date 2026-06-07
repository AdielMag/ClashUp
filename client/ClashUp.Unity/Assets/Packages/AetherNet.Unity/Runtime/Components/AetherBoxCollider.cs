using nkast.Aether.Physics2D.Dynamics;
using UnityEngine;
using AetherNet.Collision;

namespace AetherNet
{
    [AddComponentMenu("AetherNet/Box Collider")]
    public sealed class AetherBoxCollider : MonoBehaviour, IAetherColliderProvider
    {
        [SerializeField] private Vector2               _size      = Vector2.one;
        [SerializeField] private Vector2               _offset    = Vector2.zero;
        [SerializeField] private bool                  _isTrigger;
        [SerializeField] private int                   _layer;
        [SerializeField] private AetherPhysicsMaterial _material;

        void IAetherColliderProvider.AttachToBody(Body body, PhysicsWorldManager world)
        {
            float density     = _material != null ? _material.Density     : 1f;
            float friction    = _material != null ? _material.Friction    : 0.2f;
            float restitution = _material != null ? _material.Restitution : 0f;

            var simSize   = MathBridge.ToNumerics(_size)   / SimulationConstants.PixelsPerMeter;
            var simOffset = MathBridge.ToNumerics(_offset) / SimulationConstants.PixelsPerMeter;

            Fixture fixture = body.CreateRectangle(simSize.X, simSize.Y, density, AetherInterop.ToAether(simOffset));
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
