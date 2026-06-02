using MessagePack;

namespace ClashUp.Shared.MessagePackObjects
{
    [MessagePackObject]
    public sealed class MatchConfig
    {
        [Key(0)] public int NumberOfTeams { get; init; } = 2;
        [Key(1)] public int TeamSize { get; init; } = 1;
        [Key(2)] public int DurationSeconds { get; init; } = 120;
        [Key(3)] public string ObjectiveType { get; init; } = "survival";
    }
}
