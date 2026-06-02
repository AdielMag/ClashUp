using ClashUp.Shared.MessagePackObjects;
using MagicOnion;

namespace ClashUp.Shared.Services
{
    public interface IMatchmakingService : IService<IMatchmakingService>
    {
        UnaryResult<QueueTicket> EnqueueAsync(QueueRequest request);

        UnaryResult CancelAsync(QueueTicket ticket);

        UnaryResult<TicketPoll> PollTicketAsync(QueueTicket ticket);

        /// <summary>Sticky-reconnect lookup: returns the GS that owns this match.</summary>
        UnaryResult<MatchHandoff> ResolveMatchAsync(MatchId matchId);

        /// <summary>Returns a handoff if the player has an active match, or null if not.</summary>
        UnaryResult<TicketPoll> CheckActiveMatchAsync();
    }
}
