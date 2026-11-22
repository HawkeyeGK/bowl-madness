using Newtonsoft.Json.Linq;

namespace BowlPoolManager.Api.Repositories
{
    /// <summary>Test-site only data access for ResetTestData. Refuses to run outside a test environment.</summary>
    public interface ITestDataRepository
    {
        /// <summary>Deletes every document in Seasons, Picks or PoolArchives. Returns the number deleted.</summary>
        Task<int> DeleteAllAsync(string containerName);

        /// <summary>Upserts one document into Seasons, Picks, PoolArchives or Configuration.</summary>
        Task UpsertAsync(string containerName, string partitionKey, JObject document);
    }
}
