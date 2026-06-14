namespace ClashUp.Shared.Abilities
{
    public enum AbilityNodeType
    {
        Parallel,
        Hitbox,
        Projectile,
    }

    public sealed class AbilityNode
    {
        public AbilityNodeType Type { get; init; }
        public int DelayTicks { get; init; }

        // Next node to execute after this one completes (sequential chaining)
        public AbilityNode? Next { get; init; }

        // Parallel only: children that run simultaneously
        public AbilityNode[]? Children { get; init; }

        // Leaf nodes
        public HitboxConfig? Hitbox { get; init; }
        public ProjectileConfig? Projectile { get; init; }
    }
}
