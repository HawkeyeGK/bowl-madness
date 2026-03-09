using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Services
{
    public interface IHoopsGameScoringService
    {
        /// <summary>
        /// Checks if the throttle interval has expired. If so, fetches live scores from the
        /// ESPN scoreboard, updates the provided games in-place, and persists changes.
        /// </summary>
        Task CheckAndRefreshScoresAsync(List<HoopsGame> games);

        /// <summary>
        /// Updates a single game's result and propagates the winner through the bracket.
        /// </summary>
        Task ProcessGameUpdateAsync(HoopsGame game);

        /// <summary>
        /// Forces propagation of all completed games in a season, using a single in-memory list
        /// to avoid stale data issues.
        /// </summary>
        Task ForcePropagateAllAsync(string seasonId);
    }
}
