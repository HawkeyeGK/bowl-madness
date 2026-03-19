using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Api.Services
{
    public interface IEspnDataService
    {
        Task<List<TeamInfo>> GetTeamsAsync();
        Task<List<TeamInfo>> SearchTeamsAsync(string query);

        /// <summary>
        /// Fetches the live NCAAM basketball scoreboard from ESPN.
        /// Returns in-progress and recently completed games with current scores.
        /// Team names use the ESPN "location" field (e.g. "Ohio State", "TCU")
        /// which matches what CollegeBasketballData stores in homeTeam/awayTeam.
        /// </summary>
        Task<List<BasketballGameDto>> GetBasketballScoreboardAsync();
    }
}
