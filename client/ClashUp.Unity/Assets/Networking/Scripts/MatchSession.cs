using System;
using System.Threading;
using ClashUp.Shared.Hubs;
using ClashUp.Shared.MessagePackObjects;
using Cysharp.Threading.Tasks;
using Grpc.Net.Client;
using MagicOnion.Client;

namespace ClashUp.Client.Networking;

/// <summary>
/// Owns the per-match GrpcChannel + IMatchHub connection. Created when
/// MatchLifetimeScope is built; disposed when it is torn down.
/// </summary>
public sealed class MatchSession : IDisposable
{
    private readonly GameServerChannelFactory _channelFactory;
    private readonly MatchHubReceiver _receiver;
    private GrpcChannel? _channel;
    private IMatchHub? _hub;

    public MatchSession(GameServerChannelFactory channelFactory, MatchHubReceiver receiver)
    {
        _channelFactory = channelFactory;
        _receiver = receiver;
    }

    public IMatchHub Hub => _hub ?? throw new InvalidOperationException("MatchSession is not connected.");

    public MatchHubReceiver Receiver => _receiver;

    public async UniTask<JoinResult> ConnectAndJoinAsync(MatchHandoff handoff, CancellationToken ct)
    {
        _channel = _channelFactory.Create(handoff.GsEndpoint);
        _hub = await StreamingHubClient.ConnectAsync<IMatchHub, IMatchHubReceiver>(
            _channel, _receiver, cancellationToken: ct);

        return await _hub.JoinAsync(new MatchJoinRequest
        {
            MatchId = handoff.MatchId,
            MatchToken = handoff.MatchToken,
        });
    }

    public async UniTask LeaveAsync()
    {
        if (_hub is null)
        {
            return;
        }
        try
        {
            await _hub.LeaveAsync();
        }
        catch (Exception)
        {
            // Best-effort.
        }
    }

    public void Dispose()
    {
        _hub?.DisposeAsync().Forget();
        _hub = null;
        _channel?.Dispose();
        _channel = null;
    }
}
