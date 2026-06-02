using System;

namespace ClashUp.Client.Networking
{
    /// <summary>
    /// Holds the active end-user JWT and (during a match) the per-match
    /// MatchToken. Lives in AppStarter; the MatchLifetimeScope reads it.
    /// </summary>
    public sealed class SessionTokenStore
    {
        public string PlayerId { get; private set; } = string.Empty;

        public string EndUserJwt { get; private set; } = string.Empty;
        public DateTimeOffset EndUserExpiresAt { get; private set; }

        public string MatchToken { get; private set; } = string.Empty;
        public DateTimeOffset MatchTokenExpiresAt { get; private set; }

        public void UpdateEndUser(string playerId, string jwt, long expiresAtMs)
        {
            PlayerId = playerId;
            EndUserJwt = jwt;
            EndUserExpiresAt = DateTimeOffset.FromUnixTimeMilliseconds(expiresAtMs);
        }

        public void UpdateMatch(string jwt, long expiresAtMs)
        {
            MatchToken = jwt;
            MatchTokenExpiresAt = DateTimeOffset.FromUnixTimeMilliseconds(expiresAtMs);
        }

        public void ClearMatch()
        {
            MatchToken = string.Empty;
            MatchTokenExpiresAt = default;
        }
    }
}
