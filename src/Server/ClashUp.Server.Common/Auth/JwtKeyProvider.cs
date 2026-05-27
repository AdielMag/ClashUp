using System;
using System.Text;
using ClashUp.Server.Common.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClashUp.Server.Common.Auth;

public interface IJwtKeyProvider
{
    SymmetricSecurityKey EndUserKey { get; }
    SymmetricSecurityKey InterTierKey { get; }
}

public sealed class JwtKeyProvider : IJwtKeyProvider
{
    public JwtKeyProvider(IOptions<JwtOptions> options)
    {
        var opts = options.Value;
        EndUserKey = BuildKey(opts.EndUserSigningKey, nameof(opts.EndUserSigningKey));
        InterTierKey = BuildKey(opts.InterTierSigningKey, nameof(opts.InterTierSigningKey));
    }

    public SymmetricSecurityKey EndUserKey { get; }
    public SymmetricSecurityKey InterTierKey { get; }

    private static SymmetricSecurityKey BuildKey(string raw, string name)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                $"Jwt:{name} is not configured. Set it via environment variable or appsettings.");
        }

        var bytes = TryDecodeBase64(raw, out var decoded)
            ? decoded
            : Encoding.UTF8.GetBytes(raw);

        if (bytes.Length < 32)
        {
            throw new InvalidOperationException(
                $"Jwt:{name} must be at least 256 bits (32 bytes).");
        }

        return new SymmetricSecurityKey(bytes);
    }

    private static bool TryDecodeBase64(string s, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromBase64String(s);
            return true;
        }
        catch (FormatException)
        {
            bytes = Array.Empty<byte>();
            return false;
        }
    }
}
