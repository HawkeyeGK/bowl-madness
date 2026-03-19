using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Api.Services
{
    public interface IBasketballDataService
    {
        Task<List<TeamInfo>> GetTeamsAsync();

        /// <summary>
        /// Fetches live NCAAM basketball games from the CollegeBasketballData scoreboard.
        /// Only returns currently in-progress games.
        /// </summary>
        Task<List<BasketballGameDto>> GetScoreboardGamesAsync();

        /// <summary>
        /// Returns raw scoreboard JSON for admin diagnostic use.
        /// </summary>
        Task<string> GetRawScoreboardJsonAsync();

        /// <summary>
        /// Fetches all postseason (tournament) games for the given year.
        /// Returns scheduled, in-progress, and completed games — suitable for game linking.
        /// </summary>
        Task<List<BasketballGameDto>> GetTournamentGamesAsync(int year);

        /// <summary>
        /// Returns raw tournament games JSON for admin diagnostic use.
        /// </summary>
        Task<string> GetRawTournamentGamesJsonAsync(int year);
    }
}
