using Microsoft.Azure.Cosmos;
using BowlPoolManager.Api.Infrastructure;
using BowlPoolManager.Core;
using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Api.Services.ScoreFeed
{
    /// <summary>
    /// Mock feed games in the Seasons container, partition "MockFeed". The partition value keeps them apart from
    /// real season documents; every call passes it. Callers (the MockFeed endpoints) check SuperAdmin first.
    /// </summary>
    public class MockFeedStore : CosmosRepositoryBase, IMockFeedStore
    {
        public MockFeedStore(CosmosClient cosmosClient) : base(cosmosClient, Constants.Database.SeasonsContainer) { }

        public async Task<List<MockFeedGame>> GetAllAsync()
        {
            var sql = $"SELECT * FROM c WHERE c.type = '{MockFeedGame.DocumentType}'";
            return await QueryAsync<MockFeedGame>(new QueryDefinition(sql), MockFeedGame.PartitionValue);
        }

        public async Task UpsertAsync(MockFeedGame game)
        {
            game.SeasonId = MockFeedGame.PartitionValue;
            game.Type = MockFeedGame.DocumentType;
            game.Id = game.Game.ExternalId;
            await UpsertDocumentAsync(game, MockFeedGame.PartitionValue);
        }

        public async Task DeleteAsync(string id)
        {
            try { await DeleteDocumentAsync<MockFeedGame>(id, MockFeedGame.PartitionValue); }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { }
        }

        public async Task<int> ReplaceAllAsync(IEnumerable<FeedGame> games)
        {
            foreach (var existing in await GetAllAsync())
                await DeleteAsync(existing.Id);

            var count = 0;
            foreach (var g in games)
            {
                await UpsertAsync(new MockFeedGame { Game = g });
                count++;
            }
            return count;
        }
    }
}
