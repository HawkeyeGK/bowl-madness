using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Repositories
{
    public interface IGameRepository
    {
        Task AddGameAsync(BowlGame game);
        Task UpdateGameAsync(BowlGame game);
        Task<List<BowlGame>> GetGamesAsync();
        Task DeleteGameAsync(string gameId);
    }
}
