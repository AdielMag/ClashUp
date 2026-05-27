using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ClashUp.Server.Common.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClashUp.Server.Common.Auth;

public interface IJwtTokenIssuer
{
    IssuedToken IssueEndUserToken(string playerId);

    IssuedToken IssueMatchToken(string playerId, string matchId, string gsInstanceId);
}

public readonly struct IssuedToken
{
    public IssuedToken(string jwt, DateTime expiresAt)
    {
        Jwt = jwt;
        ExpiresAt = expiresAt;
    }

    public string Jwt { get; }
    public DateTime ExpiresAt { get; }
}

public sealed class JwtTokenIssuer : IJwtTokenIssuer
{
    private readonly JwtOptions _options;
    private readonly IJwtKeyProvider _keys;

    public JwtTokenIssuer(IOptions<JwtOptions> options, IJwtKeyProvider keys)
    {
        _options = options.Value;
        _keys = keys;
    }

    public IssuedToken IssueEndUserToken(string playerId)
    {
        var expires = DateTime.UtcNow.AddMinutes(_options.EndUserAccessTokenMinutes);
        var jwt = WriteToken(
            _keys.EndUserKey,
            _options.EndUserAudience,
            expires,
            new Claim(JwtClaimTypes.Sub, playerId));
        return new IssuedToken(jwt, expires);
    }

    public IssuedToken IssueMatchToken(string playerId, string matchId, string gsInstanceId)
    {
        var expires = DateTime.UtcNow.AddHours(2).AddMinutes(_options.MatchTokenGraceMinutes);
        var jwt = WriteToken(
            _keys.InterTierKey,
            _options.InterTierAudience,
            expires,
            new Claim(JwtClaimTypes.Sub, playerId),
            new Claim(JwtClaimTypes.MatchId, matchId),
            new Claim(JwtClaimTypes.GsInstanceId, gsInstanceId),
            new Claim(JwtClaimTypes.Sticky, "true"));
        return new IssuedToken(jwt, expires);
    }

    private string WriteToken(SymmetricSecurityKey key, string audience, DateTime expires, params Claim[] claims)
    {
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
