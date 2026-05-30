using MessagePack;


namespace ClashUp.Shared.MessagePackObjects
{
    [MessagePackObject]
    public class MatchJoinRequest
    {
        [Key(0)] public MatchId MatchId { get; init; }
        [Key(1)] public string MatchToken { get; init; } = "";
    }
}
