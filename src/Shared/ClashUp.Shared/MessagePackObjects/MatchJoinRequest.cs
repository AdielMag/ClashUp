using ClashUp.Shared.Characters;
using MessagePack;


namespace ClashUp.Shared.MessagePackObjects
{
    [MessagePackObject]
    public class MatchJoinRequest
    {
        [Key(0)] public MatchId MatchId { get; init; }
        [Key(1)] public string MatchToken { get; init; } = "";

        // Character the player chose pre-matchmaking. Server validates against the match's catalog
        // (unknown/empty falls back to the default character).
        [Key(2)] public CharacterId CharacterId { get; init; }
    }
}
