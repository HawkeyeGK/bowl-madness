using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Api.Services.ScoreFeed
{
    /// <summary>
    /// "Games and their scores", the same shape for every provider and sport. Read-only on purpose:
    /// the mock's admin edits go through IMockFeedStore, never through this interface.
    /// </summary>
    public interface IScoreFeed
    {
        /// <summary>Live and recent games, used by the scoring services. <paramref name="date"/> is for per-day providers (hoops); football ignores it.</summary>
        Task<IReadOnlyList<FeedGame>> GetScoreboardAsync(DateOnly? date = null);

        /// <summary>The season's postseason games, for import and linking. <paramref name="year"/> is the current season's SeasonId, never a literal.</summary>
        Task<IReadOnlyList<FeedGame>> GetPostseasonGamesAsync(int year);
    }

    /// <summary>Implemented by feeds that can return the provider's own JSON (CFBD only; used by the SuperAdmin raw dump).</summary>
    public interface IRawPostseasonJsonSource
    {
        Task<string> GetRawPostseasonJsonAsync(int year);
    }
}
