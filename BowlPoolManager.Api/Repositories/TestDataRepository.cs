using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;
using BowlPoolManager.Api.Helpers;
using BowlPoolManager.Core;

namespace BowlPoolManager.Api.Repositories
{
    public class TestDataRepository : ITestDataRepository
    {
        // Players is never reset (it keeps the owner as SuperAdmin). Configuration is only written, never wiped.
        private static readonly HashSet<string> ResettableContainers = new(StringComparer.Ordinal)
        {
            Constants.Database.SeasonsContainer,
            Constants.Database.PicksContainer,
            Constants.Database.ArchivesContainer
        };

        private static readonly HashSet<string> WritableContainers = new(ResettableContainers, StringComparer.Ordinal)
        {
            Constants.Database.ConfigurationContainer
        };

        private readonly CosmosClient _cosmosClient;
        private readonly ITestEnvironment _testEnvironment;

        public TestDataRepository(CosmosClient cosmosClient, ITestEnvironment testEnvironment)
        {
            _cosmosClient = cosmosClient;
            _testEnvironment = testEnvironment;
        }

        public async Task<int> DeleteAllAsync(string containerName)
        {
            RequireTestEnvironment();
            if (!ResettableContainers.Contains(containerName))
                throw new ArgumentException("Container cannot be reset.", nameof(containerName));

            var container = _cosmosClient.GetContainer(Constants.Database.DbName, containerName);

            // A full wipe has to be a cross-partition query, then one delete per document with its own
            // partition key (these containers all use /seasonId). Not atomic; if it fails part way, run the reset again.
            var iterator = container.GetItemQueryIterator<JObject>(new QueryDefinition("SELECT c.id, c.seasonId FROM c"));
            var deleted = 0;
            while (iterator.HasMoreResults)
            {
                foreach (var doc in await iterator.ReadNextAsync())
                {
                    var id = doc.Value<string>("id")!;
                    var key = PartitionKeyFor(doc);
                    await container.DeleteItemAsync<JObject>(id, key);
                    deleted++;
                }
            }
            return deleted;
        }

        public async Task UpsertAsync(string containerName, string partitionKey, JObject document)
        {
            RequireTestEnvironment();
            if (!WritableContainers.Contains(containerName))
                throw new ArgumentException("Container cannot be written.", nameof(containerName));

            var container = _cosmosClient.GetContainer(Constants.Database.DbName, containerName);
            await container.UpsertItemAsync(document, new PartitionKey(partitionKey));
        }

        // A missing seasonId property and an explicit JSON null are different partition keys in Cosmos.
        public static PartitionKey PartitionKeyFor(JObject doc)
        {
            if (!doc.TryGetValue("seasonId", out var token)) return PartitionKey.None;
            return token.Type == JTokenType.Null ? PartitionKey.Null : new PartitionKey(token.Value<string>());
        }

        private void RequireTestEnvironment()
        {
            if (!_testEnvironment.IsEnabled)
                throw new InvalidOperationException("Test data operations are only allowed on the test site.");
        }
    }
}
