using MagicOnion.Client;

using ClashUp.Shared.Hubs;
using ClashUp.Shared.Services;

namespace ClashUp.Client.Networking
{
    [MagicOnionClientGeneration(typeof(IPingHub), typeof(IMatchHub), typeof(IMatchmakingService), typeof(IAuthService))]
    internal static partial class MagicOnionGeneratedClientInitializer { }
}
