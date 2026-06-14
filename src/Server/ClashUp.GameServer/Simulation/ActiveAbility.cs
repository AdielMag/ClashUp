using ClashUp.Shared.Abilities;

namespace ClashUp.Server.GameServer.Simulation;

public sealed class ActiveAbility
{
    public required string CasterId { get; init; }
    public required float AimYaw { get; init; }
    public required AbilityDefinition Definition { get; init; }

    public int ElapsedTicks { get; set; }
    public bool IsFinished { get; set; }

    public required AbilityNode[] FlatNodes { get; init; }
    public required int[] NodeTicksElapsed { get; init; }
    public required bool[] NodeStarted { get; init; }
    public required bool[] NodeFinished { get; init; }
    public required HashSet<int>?[] HitboxHitEntities { get; init; }

    public static ActiveAbility Create(string casterId, float aimYaw, AbilityDefinition definition)
    {
        var flat = new List<AbilityNode>();
        if (definition.RootNode != null)
            Flatten(definition.RootNode, flat);

        int count = flat.Count;
        return new ActiveAbility
        {
            CasterId = casterId,
            AimYaw = aimYaw,
            Definition = definition,
            FlatNodes = flat.ToArray(),
            NodeTicksElapsed = new int[count],
            NodeStarted = new bool[count],
            NodeFinished = new bool[count],
            HitboxHitEntities = new HashSet<int>?[count],
        };
    }

    private static void Flatten(AbilityNode node, List<AbilityNode> result)
    {
        result.Add(node);
        if (node.Children != null)
            foreach (var child in node.Children)
                Flatten(child, result);
        if (node.Next != null)
            Flatten(node.Next, result);
    }
}
