using ClashUp.Shared.Characters;
using MessagePack;

namespace ClashUp.Shared.MessagePackObjects
{
    [MessagePackObject]
    public sealed class PlayerSummary
    {
        [Key(0)] public PlayerId Id { get; init; }
        [Key(1)] public string DisplayName { get; init; } = string.Empty;
        [Key(2)] public int TeamId { get; init; }
        [Key(3)] public int ColorSlot { get; init; }
        [Key(4)] public CharacterId CharacterId { get; init; }
    }
}
