using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Api.Services.ScoreFeed
{
    public interface IMockFeedStore
    {
        Task<List<MockFeedGame>> GetAllAsync();
        Task UpsertAsync(MockFeedGame game);
        Task DeleteAsync(string id);
        /// <summary>Deletes every mock game, then stores the given ones. Returns the number stored.</summary>
        Task<int> ReplaceAllAsync(IEnumerable<FeedGame> games);
    }
}
