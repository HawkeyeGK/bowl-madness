using Microsoft.Azure.Cosmos;
using BowlPoolManager.Api.Infrastructure;
using BowlPoolManager.Core;
using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Api.Services.ScoreFeed
{
    /// <summary>The score feed mode: one document in the Configuration container, partition key = id.</summary>
    public class ScoreFeedSettingsStore : CosmosRepositoryBase, IScoreFeedSettingsStore
    {
        public ScoreFeedSettingsStore(CosmosClient cosmosClient) : base(cosmosClient, Constants.Database.ConfigurationContainer) { }

        public async Task<ScoreFeedMode> GetModeAsync()
        {
            var doc = await GetDocumentAsync<ScoreFeedSettings>(ScoreFeedSettings.DocumentId, ScoreFeedSettings.DocumentId);
            return doc?.Mode ?? ScoreFeedMode.Live;
        }

        public async Task SetModeAsync(ScoreFeedMode mode)
        {
            var doc = new ScoreFeedSettings { Mode = mode, UpdatedUtc = DateTime.UtcNow };
            await UpsertDocumentAsync(doc, ScoreFeedSettings.DocumentId);
        }
    }
}
