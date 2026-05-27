using MessagePack;

namespace ClashUp.Shared.MessagePackObjects;

[MessagePackObject]
public sealed class LoginRequest
{
    [Key(0)] public string DeviceId { get; init; } = string.Empty;
}

[MessagePackObject]
public sealed class LoginResult
{
    [Key(0)] public PlayerId PlayerId { get; init; }
    [Key(1)] public string Jwt { get; init; } = string.Empty;
    [Key(2)] public long ExpiresAtMs { get; init; }
    [Key(3)] public string DisplayName { get; init; } = string.Empty;
}
