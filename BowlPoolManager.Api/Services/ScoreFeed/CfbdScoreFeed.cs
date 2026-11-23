using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Api.Services.ScoreFeed
{
    /// <summary>Football feed: College Football Data, through CfbdService.</summary>
    public class CfbdScoreFeed : IScoreFeed, IRawPostseasonJsonSource
    {
        private readonly ICfbdService _cfbd;

        public CfbdScoreFeed(ICfbdService cfbd) => _cfbd = cfbd;

        public async Task<IReadOnlyList<FeedGame>> GetScoreboardAsync(DateOnly? date = null)
        {
            var games = await _cfbd.GetScoreboardGamesAsync();
            return games.Select(CfbdFeedMapping.ToFeedGame).ToList();
        }

        public async Task<IReadOnlyList<FeedGame>> GetPostseasonGamesAsync(int year)
        {
            var games = await _cfbd.GetPostseasonGamesAsync(year);
            return games.Select(CfbdFeedMapping.ToFeedGame).ToList();
        }

        public Task<string> GetRawPostseasonJsonAsync(int year) => _cfbd.GetRawPostseasonGamesJsonAsync(year);
    }
}
