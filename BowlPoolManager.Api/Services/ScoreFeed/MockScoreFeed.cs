using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Api.Services.ScoreFeed
{
    /// <summary>
    /// Mock feed: reads its games from the database (see IMockFeedStore). It is only used when the stored
    /// score feed mode is Mock (see SwitchingScoreFeed); only a SuperAdmin can change that mode.
    /// </summary>
    public class MockScoreFeed : IScoreFeed
    {
        private readonly IMockFeedStore _store;

        public MockScoreFeed(IMockFeedStore store) => _store = store;

        public async Task<IReadOnlyList<FeedGame>> GetScoreboardAsync(DateOnly? date = null) =>
            (await _store.GetAllAsync()).Select(m => m.Game).ToList();

        // The year is ignored on purpose: the starting set is last season's games, and the mock must be able to
        // load and link them whichever season is current.
        public async Task<IReadOnlyList<FeedGame>> GetPostseasonGamesAsync(int year) =>
            (await _store.GetAllAsync()).Select(m => m.Game).ToList();
    }
}
