namespace ClashUp.Server.Common.Auth;

/// <summary>
/// Normative JWT claim names. Adding a new claim requires updating both
/// this class and docs/rules/jwt-auth.md.
/// </summary>
public static class JwtClaimTypes
{
    /// <summary>Player ID (Mongo accounts._id). Always present.</summary>
    public const string Sub = "sub";

    /// <summary>Match this token authorises (MatchToken only).</summary>
    public const string MatchId = "matchId";

    /// <summary>GS instance hosting the match — sticky reconnect key (MatchToken only).</summary>
    public const string GsInstanceId = "gsInstanceId";

    /// <summary>True when the token may be reused across a reconnect (MatchToken only).</summary>
    public const string Sticky = "sticky";

    /// <summary>GS instance identity (registry / inter-tier tokens).</summary>
    public const string GsId = "gsId";
}
