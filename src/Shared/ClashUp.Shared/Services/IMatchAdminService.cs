using System.Threading.Tasks;
using ClashUp.Shared.MessagePackObjects;
using MagicOnion;

namespace ClashUp.Shared.Services;

/// <summary>
/// Internal RPC hosted by GameServer. Called by Services to ask this
/// instance to prepare a new match. Auth uses an inter-tier JWT — see
/// docs/rules/jwt-auth.md.
/// </summary>
public interface IMatchAdminService : IService<IMatchAdminService>
{
    UnaryResult<MatchReady> PrepareMatchAsync(MatchProvision provision);
}
