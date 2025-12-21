using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Api.Services
{
    public interface ICfbdService
    {
        Task<List<CfbdGameDto>> GetPostseasonGamesAsync(int year);
        Task<string> GetRawPostseasonGamesJsonAsync(int year);
        Task<List<CfbdGameDto>> GetScoreboardGamesAsync();
        
        // NEW: Raw JSON for debugging
        Task<string> GetRawScoreboardJsonAsync();
    }
}
