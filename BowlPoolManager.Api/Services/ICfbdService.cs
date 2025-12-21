using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Api.Services
{
    public interface ICfbdService
    {
        Task<List<CfbdGameDto>> GetPostseasonGamesAsync(int year);
        Task<string> GetRawPostseasonGamesJsonAsync(int year);
        
        // NEW: Specific endpoint for live scoring
        Task<List<CfbdGameDto>> GetScoreboardGamesAsync();
    }
}
