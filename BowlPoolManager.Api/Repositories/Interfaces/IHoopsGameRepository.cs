using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Repositories
{
    public interface IHoopsGameRepository
    {
        Task AddGameAsync(HoopsGame game);
        Task UpdateGameAsync(HoopsGame game);
        Task<List<HoopsGame>> GetGamesAsync(string seasonId);
        Task DeleteGameAsync(string gameId, string seasonId);
        Task SaveGamesAsBatchAsync(List<HoopsGame> games, string seasonId);
        Task DeleteGamesAsBatchAsync(List<HoopsGame> games, string seasonId);
    }
}
