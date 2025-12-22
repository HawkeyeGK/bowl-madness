using Microsoft.Azure.Cosmos;
using BowlPoolManager.Api.Infrastructure;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Repositories
{
    public class PoolRepository : CosmosRepositoryBase, IPoolRepository
    {
        public PoolRepository(Container container) : base(container) { }

        public async Task AddPoolAsync(BowlPool pool) => await UpsertDocumentAsync(pool, pool.Id);
        public async Task<List<BowlPool>> GetPoolsAsync() => await GetListAsync<BowlPool>(Constants.DocumentTypes.BowlPool);
        public async Task<BowlPool?> GetPoolAsync(string id) => await GetDocumentAsync<BowlPool>(id);

        public async Task<BowlPool?> GetPoolByInviteCodeAsync(string inviteCode)
        {
            var sql = $"SELECT * FROM c WHERE c.type = '{Constants.DocumentTypes.BowlPool}' AND StringEquals(c.inviteCode, @inviteCode, true)";
            var queryDef = new QueryDefinition(sql).WithParameter("@inviteCode", inviteCode);
            var results = await QueryAsync<BowlPool>(queryDef);
            return results.FirstOrDefault();
        }
    }
}
