namespace ClashUp.Server.Common.Configuration;

/// <summary>
/// Binds to the "Jwt" configuration section. Two distinct signing keys:
/// the end-user key is presented to clients; the inter-tier key is used
/// for per-match MatchToken minting and for Services↔GameServer auth.
/// See docs/rules/jwt-auth.md.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "clashup-services";

    public string EndUserAudience { get; init; } = "clashup-client";

    public string InterTierAudience { get; init; } = "clashup-internal";

    /// <summary>Symmetric end-user signing key (base64 or raw, 256+ bits).</summary>
    public string EndUserSigningKey { get; init; } = string.Empty;

    /// <summary>Symmetric inter-tier signing key (base64 or raw, 256+ bits).</summary>
    public string InterTierSigningKey { get; init; } = string.Empty;

    public int EndUserAccessTokenMinutes { get; init; } = 60;

    public int RefreshTokenDays { get; init; } = 30;

    public int MatchTokenGraceMinutes { get; init; } = 5;
}
