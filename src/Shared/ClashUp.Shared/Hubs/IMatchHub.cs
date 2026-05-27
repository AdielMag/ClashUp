using System.Threading.Tasks;
using ClashUp.Shared.MessagePackObjects;
using MagicOnion;

namespace ClashUp.Shared.Hubs;

/// <summary>
/// The per-match StreamingHub hosted on GameServer. Client → server
/// surface. Hub methods only validate + enqueue; they never run sim or
/// broadcast — see docs/rules/magiconion-hub-discipline.md.
/// </summary>
public interface IMatchHub : IStreamingHub<IMatchHub, IMatchHubReceiver>
{
    Task<JoinResult> JoinAsync(MatchJoinRequest request);

    Task LeaveAsync();

    Task SubmitInputAsync(InputCommand command);

    Task<PongResult> PingAsync(long clientStampMs);
}
