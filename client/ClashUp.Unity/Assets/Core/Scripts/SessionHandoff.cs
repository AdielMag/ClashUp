using System;

namespace ClashUp.Client.Core;

/// <summary>
/// Plain value passed from AppStarter to CoreStarter when the user enters
/// the gameplay flow. Crossing the scope boundary via DTO rather than
/// scope inheritance is intentional — see docs/rules/vcontainer-scopes.md.
/// </summary>
public readonly struct SessionHandoff
{
    public SessionHandoff(string playerId, string jwt, DateTimeOffset jwtExpiresAt)
    {
        PlayerId = playerId;
        Jwt = jwt;
        JwtExpiresAt = jwtExpiresAt;
    }

    public string PlayerId { get; }
    public string Jwt { get; }
    public DateTimeOffset JwtExpiresAt { get; }
}
