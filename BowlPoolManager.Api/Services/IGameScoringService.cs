using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Services
{
    public interface IGameScoringService
    {
        /// <summary>
        /// Checks if the cache duration has expired. If so, fetches new scores from CFBD,
        /// updates the provided games list in-place, and persists changes to Cosmos DB.
        /// </summary>
        Task CheckAndRefreshScoresAsync(List<BowlGame> games);

        /// <summary>
        /// Returns the timestamp of the last successful score refresh.
        /// </summary>
        DateTime GetLastRefreshTime();
    }
}
