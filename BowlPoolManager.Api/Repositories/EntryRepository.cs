using Microsoft.Azure.Cosmos;
using BowlPoolManager.Api.Infrastructure;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Repositories
{
    public class EntryRepository : CosmosRepositoryBase, IEntryRepository
    {
        public EntryRepository(CosmosClient cosmosClient) : base(cosmosClient, Constants.Database.PicksContainer) { }

        public async Task AddEntryAsync(BracketEntry entry) => await UpsertDocumentAsync(entry, entry.SeasonId);

        public async Task<List<BracketEntry>> GetEntriesAsync(string? poolId = null)
        {
            var sql = $"SELECT * FROM c WHERE c.type = '{Constants.DocumentTypes.BracketEntry}'";
            if (!string.IsNullOrEmpty(poolId))
            {
                sql += " AND c.poolId = @poolId";
                var queryDef = new QueryDefinition(sql).WithParameter("@poolId", poolId);
                return await QueryAsync<BracketEntry>(queryDef);
            }
            return await QueryAsync<BracketEntry>(new QueryDefinition(sql));
        }

        public async Task<BracketEntry?> GetEntryAsync(string id, string seasonId)
        {
             return await GetDocumentAsync<BracketEntry>(id, seasonId);
        }

        public async Task DeleteEntryAsync(string id, string seasonId)
        {
             await DeleteDocumentAsync<BracketEntry>(id, seasonId);
        }

        public async Task<List<BracketEntry>> GetEntriesForUserAsync(string userId, string poolId)
        {
            var sql = $"SELECT * FROM c WHERE c.type = '{Constants.DocumentTypes.BracketEntry}' AND c.userId = @userId";
            QueryDefinition queryDef;

            if (!string.IsNullOrEmpty(poolId))
            {
                sql += " AND c.poolId = @poolId";
                queryDef = new QueryDefinition(sql).WithParameter("@userId", userId).WithParameter("@poolId", poolId);
            }
            else
            {
                queryDef = new QueryDefinition(sql).WithParameter("@userId", userId);
            }
            return await QueryAsync<BracketEntry>(queryDef);
        }

        public async Task<bool> IsBracketNameTakenAsync(string poolId, string bracketName, string? excludeId = null)
        {
            var sql = $"SELECT VALUE COUNT(1) FROM c WHERE c.type = '{Constants.DocumentTypes.BracketEntry}' " +
                      "AND c.poolId = @poolId AND StringEquals(c.playerName, @name, true)";

            var queryDef = new QueryDefinition(sql).WithParameter("@poolId", poolId).WithParameter("@name", bracketName);

            if (!string.IsNullOrEmpty(excludeId))
            {
                sql += " AND c.id != @excludeId";
                queryDef = new QueryDefinition(sql)
                    .WithParameter("@poolId", poolId)
                    .WithParameter("@name", bracketName)
                    .WithParameter("@excludeId", excludeId);
            }

            var iterator = _container.GetItemQueryIterator<int>(queryDef);
            if (iterator.HasMoreResults)
            {
                var result = await iterator.ReadNextAsync();
                return result.FirstOrDefault() > 0;
            }
            return false;
        }
    }
}
