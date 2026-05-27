using System.Threading.Tasks;
using ClashUp.Shared.MessagePackObjects;
using MagicOnion;

namespace ClashUp.Shared.Hubs
{
    /// <summary>
    /// Smoke-test StreamingHub. Used during bring-up to verify the
    /// MagicOnion + MessagePack + transport path end-to-end. Not a
    /// production hub.
    /// </summary>
    public interface IPingHub : IStreamingHub<IPingHub, IPingHubReceiver>
    {
        Task<PongResult> PingAsync(PingRequest request);
    }
}
