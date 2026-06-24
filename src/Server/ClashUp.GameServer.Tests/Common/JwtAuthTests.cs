using ClashUp.Server.Common.Auth;
using ClashUp.Server.Common.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ClashUp.Server.GameServer.Tests.Common;

public class JwtKeyProviderTests
{
    private static IOptions<JwtOptions> Opts(JwtOptions o) => Options.Create(o);

    [Fact]
    public void Throws_WhenKeyIsMissing()
    {
        var act = () => new JwtKeyProvider(Opts(new JwtOptions
        {
            EndUserSigningKey = "",
            InterTierSigningKey = new string('k', 32),
        }));
        act.Should().Throw<InvalidOperationException>().WithMessage("*EndUserSigningKey*");
    }

    [Fact]
    public void Throws_WhenKeyIsTooShort()
    {
        var act = () => new JwtKeyProvider(Opts(new JwtOptions
        {
            EndUserSigningKey = new string('k', 32),
            InterTierSigningKey = "tooshort", // < 32 bytes
        }));
        act.Should().Throw<InvalidOperationException>().WithMessage("*256 bits*");
    }

    [Fact]
    public void BuildsKeys_FromRawUtf8()
    {
        // Hyphens make these invalid base64, so the provider falls back to raw UTF8 bytes (1 byte/char).
        var provider = new JwtKeyProvider(Opts(new JwtOptions
        {
            EndUserSigningKey = "end-user-raw-signing-key-with-enough-length",
            InterTierSigningKey = "inter-tier-raw-signing-key-with-enough-length",
        }));

        provider.EndUserKey.KeySize.Should().BeGreaterThanOrEqualTo(256);
        provider.InterTierKey.KeySize.Should().BeGreaterThanOrEqualTo(256);
    }
}

public class JwtTokenIssuerValidatorTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "clashup-services",
        EndUserAudience = "clashup-client",
        InterTierAudience = "clashup-internal",
        EndUserSigningKey = new string('e', 48),
        InterTierSigningKey = new string('i', 48),
    };

    private static (JwtTokenIssuer issuer, MatchTokenValidator validator) Build()
    {
        var opts = Microsoft.Extensions.Options.Options.Create(Options);
        var keys = new JwtKeyProvider(opts);
        return (new JwtTokenIssuer(opts, keys), new MatchTokenValidator(opts, keys));
    }

    [Fact]
    public void MatchToken_RoundTrips_AllClaims()
    {
        var (issuer, validator) = Build();

        var token = issuer.IssueMatchToken("player-7", "match-42", "gs-1");
        token.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        var claims = validator.Validate(token.Jwt);
        claims.PlayerId.Should().Be("player-7");
        claims.MatchId.Should().Be("match-42");
        claims.GsInstanceId.Should().Be("gs-1");
        claims.Sticky.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsTamperedToken()
    {
        var (issuer, validator) = Build();
        var token = issuer.IssueMatchToken("p", "m", "gs");

        // Flip a character in the signature segment.
        var tampered = token.Jwt[..^2] + (token.Jwt[^1] == 'a' ? "bb" : "aa");

        validator.Invoking(v => v.Validate(tampered))
            .Should().Throw<Exception>();
    }

    [Fact]
    public void Validate_RejectsEndUserToken_WrongAudience()
    {
        var (issuer, validator) = Build();
        // End-user tokens are signed with a different key + audience than the validator accepts.
        var endUser = issuer.IssueEndUserToken("p");

        validator.Invoking(v => v.Validate(endUser.Jwt))
            .Should().Throw<SecurityTokenException>();
    }
}
