using System;
using System.Threading;

using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Services;

using Cysharp.Threading.Tasks;

namespace ClashUp.Client.Networking.Networking.Scripts;

public abstract sealed class MatchmakingClient
{
    private readonly MagicOnionChannelProvider _channels;

    public MatchmakingClient(MagicOnionChannelProvider channels)
    {
        _channels = channels;
    }

    public async UniTask<MatchHandoff> EnqueueAndWaitAsync(QueueRequest request, CancellationToken ct)
    {
        var client = MagicOnionClient.Create<IMatchmakingService>(_channels.Services);
        var ticket = await client.EnqueueAsync(request);

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            await UniTask.Delay(TimeSpan.FromMilliseconds(250), cancellationToken: ct);
            var poll = await client.PollTicketAsync(ticket);
            switch (poll.Status)
            {
                case TicketStatus.Matched when poll.Handoff is not null:
                    return poll.Handoff;
                case TicketStatus.Cancelled:
                    throw new OperationCanceledException("Matchmaking ticket cancelled.");
                case TicketStatus.Failed:
                    throw new InvalidOperationException($"Matchmaking failed: {poll.FailureReason}");
                default:
                    continue;
            }
        }
    }
}
