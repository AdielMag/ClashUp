using ClashUp.Server.Common.Auth;
using ClashUp.Server.Services.Persistence;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Services;
using MagicOnion;
using MagicOnion.Server;
using MongoDB.Driver;

namespace ClashUp.Server.Services.Services;

public sealed class AuthServiceImpl : ServiceBase<IAuthService>, IAuthService
{
    private readonly IAccountRepository _accounts;
    private readonly IJwtTokenIssuer _tokens;

    public AuthServiceImpl(IAccountRepository accounts, IJwtTokenIssuer tokens)
    {
        _accounts = accounts;
        _tokens = tokens;
    }

    public async UnaryResult<LoginResult> LoginWithDeviceIdAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            throw new InvalidOperationException("DeviceId is required.");
        }

        var ct = Context.CallContext.CancellationToken;
        var existing = await _accounts.GetByDeviceIdAsync(request.DeviceId, ct);
        var account = existing ?? await CreateAccountAsync(request.DeviceId, ct);

        var token = _tokens.IssueEndUserToken(account.PlayerId);
        return new LoginResult
        {
            PlayerId = new PlayerId(account.PlayerId),
            Jwt = token.Jwt,
            ExpiresAtMs = new DateTimeOffset(token.ExpiresAt).ToUnixTimeMilliseconds(),
            DisplayName = account.DisplayName,
        };
    }

    private async Task<AccountDoc> CreateAccountAsync(string deviceId, CancellationToken ct)
    {
        var playerId = Guid.NewGuid().ToString("N");
        var doc = new AccountDoc
        {
            PlayerId = playerId,
            DeviceId = deviceId,
            DisplayName = $"Player-{playerId[..6]}",
            CreatedAt = DateTime.UtcNow,
        };

        try
        {
            return await _accounts.CreateAsync(doc, ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Raced another login for the same device; re-read.
            return (await _accounts.GetByDeviceIdAsync(deviceId, ct))!;
        }
    }
}
