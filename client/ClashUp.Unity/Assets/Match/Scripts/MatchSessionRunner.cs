using System;
using System.Threading;
using ClashUp.Client.Networking;
using ClashUp.Shared.MessagePackObjects;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace ClashUp.Client.Match;

/// <summary>
/// Drives the match session lifecycle once MatchLifetimeScope is built.
/// Expects the handoff to be present on the scope (set by whoever
/// instantiated the scope after matchmaking returned).
/// </summary>
public sealed class MatchSessionRunner : IAsyncStartable, IDisposable
{
    private readonly MatchSession _session;
    private readonly MatchHandoffHolder _handoff;

    public MatchSessionRunner(MatchSession session, MatchHandoffHolder handoff)
    {
        _session = session;
        _handoff = handoff;
    }

    public async UniTask StartAsync(CancellationToken cancellation)
    {
        if (string.IsNullOrEmpty(_handoff.Value.MatchToken))
        {
            Debug.LogError("[Match] No handoff present in scope; cannot start session.");
            return;
        }

        try
        {
            var join = await _session.ConnectAndJoinAsync(_handoff.Value, cancellation);
            Debug.Log($"[Match] Joined match {_handoff.Value.MatchId}. tickRate={join.TickRateHz}Hz");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Match] Connect/Join failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _session.LeaveAsync().Forget();
        _session.Dispose();
    }
}

/// <summary>
/// Tiny container for the MatchHandoff so CoreStarter can hand it to
/// the child MatchLifetimeScope without leaking it onto AppStarter.
/// </summary>
public sealed class MatchHandoffHolder
{
    public MatchHandoff Value { get; set; } = new();
}
