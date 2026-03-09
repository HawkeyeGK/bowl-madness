using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Api.Services
{
    public interface IBasketballDataService
    {
        Task<List<TeamInfo>> GetTeamsAsync();

        /// <summary>
        /// Fetches live NCAAM basketball games from the CollegeBasketballData scoreboard.
        /// </summary>
        Task<List<BasketballGameDto>> GetScoreboardGamesAsync();

        /// <summary>
        /// Returns raw scoreboard JSON for admin diagnostic use.
        /// </summary>
        Task<string> GetRawScoreboardJsonAsync();
    }
}
