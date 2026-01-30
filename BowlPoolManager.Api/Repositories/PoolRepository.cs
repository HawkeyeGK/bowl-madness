using Microsoft.Azure.Cosmos;
using BowlPoolManager.Api.Infrastructure;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Repositories
{
    public class PoolRepository : CosmosRepositoryBase, IPoolRepository
    {
        public PoolRepository(CosmosClient cosmosClient) : base(cosmosClient, Constants.Database.SeasonsContainer) { }

        public async Task AddPoolAsync(BowlPool pool) => await UpsertDocumentAsync(pool, pool.SeasonId);
        public async Task<List<BowlPool>> GetPoolsAsync(string? seasonId = null)
        {
            var sql = $"SELECT * FROM c WHERE c.type = '{Constants.DocumentTypes.BowlPool}'";
            QueryDefinition queryDef = new QueryDefinition(sql);

            if (!string.IsNullOrEmpty(seasonId))
            {
                sql += " AND c.seasonId = @seasonId";
                queryDef = new QueryDefinition(sql).WithParameter("@seasonId", seasonId);
            }
            
            return await QueryAsync<BowlPool>(queryDef, seasonId);
        }
        
        public async Task<BowlPool?> GetPoolAsync(string id)
        {
             var sql = "SELECT * FROM c WHERE c.id = @id";
             var queryDef = new QueryDefinition(sql).WithParameter("@id", id);
             var results = await QueryAsync<BowlPool>(queryDef);
             return results.FirstOrDefault();
        }

        public async Task<BowlPool?> GetPoolByInviteCodeAsync(string inviteCode)
        {
            var sql = $"SELECT * FROM c WHERE c.type = '{Constants.DocumentTypes.BowlPool}' AND StringEquals(c.inviteCode, @inviteCode, true) AND c.isArchived != true";
            var queryDef = new QueryDefinition(sql).WithParameter("@inviteCode", inviteCode);
            var results = await QueryAsync<BowlPool>(queryDef);
            return results.FirstOrDefault();
        }

        public async Task DeletePoolAsync(string poolId)
        {
             var pool = await GetPoolAsync(poolId);
             if (pool != null)
             {
                 await DeleteDocumentAsync<BowlPool>(poolId, pool.SeasonId);
             }
        }
    }
}
