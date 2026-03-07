using BowlPoolManager.Api.Infrastructure;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;
using Microsoft.Azure.Cosmos;

namespace BowlPoolManager.Api.Repositories
{
    public class ConfigurationRepository : CosmosRepositoryBase, IConfigurationRepository
    {
        public ConfigurationRepository(CosmosClient cosmosClient) : base(cosmosClient, Constants.Database.ConfigurationContainer) { }

        public async Task<TeamConfig?> GetTeamConfigAsync()
        {
            return await GetDocumentAsync<TeamConfig>(Constants.ConfigDocumentIds.FbsTeamConfig, Constants.ConfigDocumentIds.FbsTeamConfig);
        }

        public async Task<TeamConfig?> GetBasketballTeamConfigAsync()
        {
            return await GetDocumentAsync<TeamConfig>(Constants.ConfigDocumentIds.BasketballTeamConfig, Constants.ConfigDocumentIds.BasketballTeamConfig);
        }

        public async Task SaveBasketballTeamConfigAsync(TeamConfig config)
        {
            await UpsertDocumentAsync(config, config.Id);
        }
    }
}
