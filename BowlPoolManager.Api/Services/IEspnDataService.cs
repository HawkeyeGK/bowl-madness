using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Api.Services
{
    public interface IEspnDataService
    {
        Task<List<TeamInfo>> GetTeamsAsync();
        Task<List<TeamInfo>> SearchTeamsAsync(string query);

        /// <summary>
        /// Fetches live NCAAM basketball games from the ESPN scoreboard API.
        /// </summary>
        Task<List<EspnScoreboardGameDto>> GetScoreboardGamesAsync();
    }
}
