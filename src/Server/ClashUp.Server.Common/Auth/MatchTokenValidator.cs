using System.IdentityModel.Tokens.Jwt;
using ClashUp.Server.Common.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClashUp.Server.Common.Auth;

public readonly struct MatchTokenClaims
{
    public MatchTokenClaims(string playerId, string matchId, string gsInstanceId, bool sticky)
    {
        PlayerId = playerId;
        MatchId = matchId;
        GsInstanceId = gsInstanceId;
        Sticky = sticky;
    }

    public string PlayerId { get; }
    public string MatchId { get; }
    public string GsInstanceId { get; }
    public bool Sticky { get; }
}

public interface IMatchTokenValidator
{
    /// <summary>
    /// Validates a MatchToken JWT signed with the inter-tier key.
    /// Throws SecurityTokenException on any failure.
    /// </summary>
    MatchTokenClaims Validate(string jwt);
}

public sealed class MatchTokenValidator : IMatchTokenValidator
{
    private readonly TokenValidationParameters _parameters;

    public MatchTokenValidator(IOptions<JwtOptions> options, IJwtKeyProvider keys)
    {
        var opts = options.Value;
        _parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = opts.Issuer,
            ValidateAudience = true,
            ValidAudience = opts.InterTierAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = keys.InterTierKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(15),
        };
    }

    public MatchTokenClaims Validate(string jwt)
    {
        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(jwt, _parameters, out _);

        var playerId = principal.FindFirst(JwtClaimTypes.Sub)?.Value
            ?? throw new SecurityTokenException("MatchToken missing sub claim.");
        var matchId = principal.FindFirst(JwtClaimTypes.MatchId)?.Value
            ?? throw new SecurityTokenException("MatchToken missing matchId claim.");
        var gsInstanceId = principal.FindFirst(JwtClaimTypes.GsInstanceId)?.Value
            ?? throw new SecurityTokenException("MatchToken missing gsInstanceId claim.");
        var sticky = string.Equals(principal.FindFirst(JwtClaimTypes.Sticky)?.Value, "true", StringComparison.OrdinalIgnoreCase);

        return new MatchTokenClaims(playerId, matchId, gsInstanceId, sticky);
    }
}
