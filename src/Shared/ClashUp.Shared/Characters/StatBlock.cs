using MessagePack;

namespace ClashUp.Shared.Characters
{
    [MessagePackObject]
    public sealed class StatBlock
    {
        [Key(0)] public float MaxHealth { get; init; } = 100f;
        [Key(1)] public float Damage { get; init; } = 10f;
        [Key(2)] public float MoveSpeed { get; init; } = 5f;
    }
}
