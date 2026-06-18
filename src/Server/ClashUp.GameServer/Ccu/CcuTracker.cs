using ClashUp.Server.Common.Ccu;
using ClashUp.Server.GameServer.Match;
using Microsoft.Extensions.Options;

namespace ClashUp.Server.GameServer.Ccu;

/// <summary>
/// Match-aware <see cref="ICcuTracker"/> for the GameServer tier. A player is
/// counted from their first connect until a disconnect grace period elapses
/// without a reconnect. Backed by the shared <see cref="GraceCcuCounter"/>.
/// </summary>
public sealed class CcuTracker : ICcuTracker, IDisposable
{
    private readonly GraceCcuCounter _counter;

    public CcuTracker(IOptions<GameServerOptions> options, ILogger<CcuTracker> logger)
    {
        _counter = new GraceCcuCounter(
            TimeSpan.FromSeconds(Math.Max(0, options.Value.CcuGracePeriodSeconds)), logger);
    }

    public int CurrentCcu => _counter.Count;

    public void PlayerConnected(string playerId) => _counter.Add(playerId);

    public void PlayerDisconnected(string playerId) => _counter.Remove(playerId);

    public void Dispose() => _counter.Dispose();
}
