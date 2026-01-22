using Microsoft.Azure.Cosmos;
using BowlPoolManager.Api.Infrastructure;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Repositories
{
    public class ArchiveRepository : CosmosRepositoryBase, IArchiveRepository
    {
        // Using SeasonId as partition key for consistency and querying ease
        public ArchiveRepository(CosmosClient cosmosClient) : base(cosmosClient, Constants.Database.ArchivesContainer) { }

        public async Task AddArchiveAsync(PoolArchive archive) => await UpsertDocumentAsync(archive, archive.SeasonId);
        
        public async Task<PoolArchive?> GetArchiveAsync(string id)
        {
             // This might be cross-partition if we don't know SeasonId. 
             // Ideally we should pass seasonId to GetArchiveAsync or query by SQL.
             var sql = "SELECT * FROM c WHERE c.id = @id";
             var queryDef = new QueryDefinition(sql).WithParameter("@id", id);
             var results = await QueryAsync<PoolArchive>(queryDef);
             return results.FirstOrDefault();
        }

        public async Task<List<PoolArchive>> GetArchivesBySeasonAsync(string seasonId)
        {
             var sql = "SELECT * FROM c WHERE c.seasonId = @seasonId OR c.season = @seasonId"; // Handle both potential formats if needed, but Model uses SeasonId string
             var queryDef = new QueryDefinition(sql).WithParameter("@seasonId", seasonId);
             
             // We can optimize this by using the PartitionKey in the request options within QueryAsync if structured that way,
             // but QueryAsync in Base likely takes a query definition. 
             // If Base doesn't support forcing PK, the engine handles it.
             
             return await QueryAsync<PoolArchive>(queryDef);
        }
    }
}
