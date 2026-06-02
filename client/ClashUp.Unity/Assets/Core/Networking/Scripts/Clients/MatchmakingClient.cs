using System;
using System.Threading;
using System.Threading.Tasks;

using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Services;

using Grpc.Core;
using Grpc.Core.Interceptors;

using Cysharp.Threading.Tasks;

using MagicOnion.Client;

namespace ClashUp.Client.Networking
{
    public class MatchmakingClient
    {
        private readonly MagicOnionChannelProvider _channels;
        private readonly SessionTokenStore _tokens;

        public MatchmakingClient(MagicOnionChannelProvider channels, SessionTokenStore tokens)
        {
            _channels = channels;
            _tokens = tokens;
        }

        public async UniTask<MatchHandoff> CheckActiveMatchAsync(CancellationToken ct)
        {
            var client = CreateClient();
            var poll = await client.CheckActiveMatchAsync();

            if (poll.Status == TicketStatus.Matched && poll.Handoff is not null)
                return poll.Handoff;

            return null;
        }

        public async UniTask<MatchHandoff> EnqueueAndWaitAsync(QueueRequest request, CancellationToken ct)
        {
            var client = CreateClient();
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

        private IMatchmakingService CreateClient()
        {
            var invoker = _channels.Services.Intercept(new PlayerIdInterceptor(_tokens.PlayerId));
            return MagicOnionClient.Create<IMatchmakingService>(invoker);
        }
    }

    internal sealed class PlayerIdInterceptor : Interceptor
    {
        private readonly string _playerId;

        public PlayerIdInterceptor(string playerId) => _playerId = playerId;

        private Metadata AddHeader(Metadata headers)
        {
            var h = headers ?? new Metadata();
            h.Add("x-clashup-player", _playerId);
            return h;
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            var newOptions = context.Options.WithHeaders(AddHeader(context.Options.Headers));
            var newContext = new ClientInterceptorContext<TRequest, TResponse>(
                context.Method, context.Host, newOptions);
            return continuation(request, newContext);
        }
    }
}
