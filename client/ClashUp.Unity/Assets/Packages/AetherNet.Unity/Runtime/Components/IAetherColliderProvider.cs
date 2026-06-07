using nkast.Aether.Physics2D.Dynamics;

namespace AetherNet
{
    /// <summary>
    /// Implemented by AetherBoxCollider, AetherCircleCollider, AetherPolygonCollider.
    /// Called once by AetherViewManager during scene initialization.
    /// After AttachToBody returns, the component goes dormant — no further runtime work.
    /// </summary>
    public interface IAetherColliderProvider
    {
        void AttachToBody(Body body, PhysicsWorldManager world);
    }
}
