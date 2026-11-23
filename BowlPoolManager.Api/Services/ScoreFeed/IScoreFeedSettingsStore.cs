using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Api.Services.ScoreFeed
{
    public interface IScoreFeedSettingsStore
    {
        /// <summary>The stored mode; Live when nothing is stored.</summary>
        Task<ScoreFeedMode> GetModeAsync();
        Task SetModeAsync(ScoreFeedMode mode);
    }
}
