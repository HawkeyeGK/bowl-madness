using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Services
{
    public interface IHoopsGameScoringService
    {
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
