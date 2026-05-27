namespace ClashUp.Server.Services.Persistence;

public interface IMatchRepository
{
    Task InsertAsync(MatchDoc doc, CancellationToken cancellationToken);
    Task SetStateAsync(string matchId, string state, CancellationToken cancellationToken);
    Task<MatchDoc?> GetByIdAsync(string matchId, CancellationToken cancellationToken);
    Task<MatchDoc?> FindActiveForPlayerAsync(string playerId, CancellationToken cancellationToken);
}
