using System.Threading;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Services;
using Cysharp.Threading.Tasks;
using MagicOnion.Client;

namespace ClashUp.Client.Networking;

/// <summary>
/// Sticky-reconnect path: asks Services where a match is hosted now.
/// Used when the per-match GrpcChannel drops and the client lost the
/// original handoff (e.g. after an app restart).
/// </summary>
public sealed class ResolveMatchClient
{
    private readonly MagicOnionChannelProvider _channels;

    public ResolveMatchClient(MagicOnionChannelProvider channels)
    {
        _channels = channels;
    }

    public async UniTask<MatchHandoff> ResolveAsync(MatchId matchId, CancellationToken ct)
    {
        var client = MagicOnionClient.Create<IMatchmakingService>(_channels.Services);
        return await client.ResolveMatchAsync(matchId);
    }
}
