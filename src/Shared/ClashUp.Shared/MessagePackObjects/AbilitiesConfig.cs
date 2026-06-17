using System.Collections.Generic;
using ClashUp.Shared.Abilities;
using MessagePack;

namespace ClashUp.Shared.MessagePackObjects
{
    [MessagePackObject]
    public sealed class AbilityClientInfo
    {
        [Key(0)] public AbilityId Id { get; init; }
        [Key(1)] public TelegraphConfig? Telegraph { get; init; }
        [Key(2)] public float AutoRange { get; init; }
        [Key(3)] public CastMode CastMode { get; init; }

        // Damage footprint shown by the triggered cast flash (derived from the hitbox).
        [Key(4)] public TelegraphConfig? CastShape { get; init; }
    }

    [MessagePackObject]
    public sealed class AbilitiesConfig
    {
        [Key(0)] public IReadOnlyList<AbilityClientInfo> Abilities { get; init; } = System.Array.Empty<AbilityClientInfo>();

        public static AbilitiesConfig Default { get; } = new()
        {
            Abilities = new[]
            {
                new AbilityClientInfo
                {
                    Id = new AbilityId("brawler_punch"),
                    Telegraph = new TelegraphConfig
                    {
                        Shape = TelegraphShape.CircleAroundCaster,
                        Radius = 4.0f,
                    },
                    CastShape = new TelegraphConfig
                    {
                        Shape = TelegraphShape.Capsule,
                        Length = 3.0f,
                        Width = 2.0f,
                    },
                    AutoRange = 4.0f,
                    CastMode = CastMode.Instant,
                },
                new AbilityClientInfo
                {
                    Id = new AbilityId("brawler_charge"),
                    Telegraph = new TelegraphConfig
                    {
                        Shape = TelegraphShape.ForwardCone,
                        Length = 3.5f,
                        Angle = 90f,
                    },
                    CastShape = new TelegraphConfig
                    {
                        Shape = TelegraphShape.ForwardCone,
                        Length = 3.5f,
                        Angle = 90f,
                    },
                    AutoRange = 4.0f,
                    CastMode = CastMode.Aimed,
                },
            },
        };
    }
}
