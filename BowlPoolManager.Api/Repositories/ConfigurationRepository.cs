using Microsoft.Azure.Cosmos;
using BowlPoolManager.Api.Infrastructure;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Repositories
{
    public class ConfigurationRepository : CosmosRepositoryBase, IConfigurationRepository
    {
        public ConfigurationRepository(CosmosClient cosmosClient) : base(cosmosClient, Constants.Database.ConfigurationContainer) { }

        public async Task<TeamConfig?> GetTeamConfigAsync()
        {
            // Fetch the specific configuration document
            // We know the ID and PartitionKey are "Config_Teams_FBS"
            var sql = "SELECT * FROM c WHERE c.id = @id";
            var queryDef = new QueryDefinition(sql).WithParameter("@id", "Config_Teams_FBS");
            
            // Note: If the Base class supports passing PartitionKey to QueryAsync, we could optimize,
            // but for a singleton config, standard query is fine.
            var results = await QueryAsync<TeamConfig>(queryDef);
            return results.FirstOrDefault();
        }
    }
}
