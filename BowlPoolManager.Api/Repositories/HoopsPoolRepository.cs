using Microsoft.Azure.Cosmos;
using BowlPoolManager.Api.Infrastructure;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Repositories
{
    public class HoopsPoolRepository : CosmosRepositoryBase, IHoopsPoolRepository
    {
        public HoopsPoolRepository(CosmosClient cosmosClient) : base(cosmosClient, Constants.Database.SeasonsContainer) { }

        public async Task AddPoolAsync(HoopsPool pool) => await UpsertDocumentAsync(pool, pool.SeasonId);

        public async Task<List<HoopsPool>> GetPoolsAsync(string? seasonId = null)
        {
            var sql = $"SELECT * FROM c WHERE c.type = '{Constants.DocumentTypes.HoopsPool}'";
            QueryDefinition queryDef = new QueryDefinition(sql);

            if (!string.IsNullOrEmpty(seasonId))
            {
                sql += " AND c.seasonId = @seasonId";
                queryDef = new QueryDefinition(sql).WithParameter("@seasonId", seasonId);
            }

            return await QueryAsync<HoopsPool>(queryDef, seasonId);
        }

        public async Task<HoopsPool?> GetPoolAsync(string id)
        {
            var sql = "SELECT * FROM c WHERE c.id = @id";
            var queryDef = new QueryDefinition(sql).WithParameter("@id", id);
            var results = await QueryAsync<HoopsPool>(queryDef);
            return results.FirstOrDefault();
        }

        public async Task<HoopsPool?> GetPoolByInviteCodeAsync(string inviteCode)
        {
            var sql = $"SELECT * FROM c WHERE c.type = '{Constants.DocumentTypes.HoopsPool}' AND StringEquals(c.inviteCode, @inviteCode, true) AND c.isArchived != true";
            var queryDef = new QueryDefinition(sql).WithParameter("@inviteCode", inviteCode);
            var results = await QueryAsync<HoopsPool>(queryDef);
            return results.FirstOrDefault();
        }

        public async Task DeletePoolAsync(string poolId)
        {
            var pool = await GetPoolAsync(poolId);
            if (pool != null)
            {
                await DeleteDocumentAsync<HoopsPool>(poolId, pool.SeasonId);
            }
        }
    }
}
