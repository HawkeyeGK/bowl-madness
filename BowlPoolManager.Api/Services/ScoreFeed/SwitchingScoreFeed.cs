using System.Text.Json;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Api.Services.ScoreFeed
{
    /// <summary>
    /// The IScoreFeed the app uses. Reads the stored mode on every call and forwards to the live or the mock feed.
    /// A missing or unreadable mode means Live, so a fault can never put a site on mock data.
    /// </summary>
    public class SwitchingScoreFeed : IScoreFeed, IRawPostseasonJsonSource
    {
        private readonly IScoreFeed _live;
        private readonly IScoreFeed _mock;
        private readonly IScoreFeedSettingsStore _settings;
        private readonly ILogger<SwitchingScoreFeed> _logger;

        public SwitchingScoreFeed(IScoreFeed live, IScoreFeed mock, IScoreFeedSettingsStore settings, ILogger<SwitchingScoreFeed> logger)
        {
            _live = live;
            _mock = mock;
            _settings = settings;
            _logger = logger;
        }

        public async Task<IReadOnlyList<FeedGame>> GetScoreboardAsync(DateOnly? date = null) =>
            await (await PickAsync()).GetScoreboardAsync(date);

        public async Task<IReadOnlyList<FeedGame>> GetPostseasonGamesAsync(int year) =>
            await (await PickAsync()).GetPostseasonGamesAsync(year);

        public async Task<string> GetRawPostseasonJsonAsync(int year)
        {
            var feed = await PickAsync();
            if (feed is IRawPostseasonJsonSource raw) return await raw.GetRawPostseasonJsonAsync(year);

            var games = (await feed.GetPostseasonGamesAsync(year)).Select(CfbdFeedMapping.ToCfbdDto).ToList();
            return JsonSerializer.Serialize(games);
        }

        private async Task<IScoreFeed> PickAsync()
        {
            try
            {
                return await _settings.GetModeAsync() == ScoreFeedMode.Mock ? _mock : _live;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read the score feed mode; using the live feed.");
                return _live;
            }
        }
    }
}
