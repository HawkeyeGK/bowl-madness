using Microsoft.Azure.Cosmos;
using BowlPoolManager.Api.Infrastructure;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Repositories
{
    public class SeasonRepository : CosmosRepositoryBase, ISeasonRepository
    {
        public SeasonRepository(CosmosClient cosmosClient) : base(cosmosClient, Constants.Database.SeasonsContainer) { }

        public async Task<List<Season>> GetSeasonsAsync()
        {
            // Cross-partition query to get all seasons
            // Filter by type to ensure we don't pick up Games if they are in the same container.
            // Assumption: Season documents have type='Season'.
            var sql = "SELECT * FROM c WHERE c.type = 'Season'";
            return await QueryAsync<Season>(new QueryDefinition(sql));
        }

        public async Task UpsertSeasonAsync(Season season)
        {
            if (season.IsCurrent)
            {
                // Find other current seasons to deactivate them
                // Cross-partition query
                var sql = "SELECT * FROM c WHERE c.type = 'Season' AND c.isCurrent = true AND c.id != @id";
                var queryDef = new QueryDefinition(sql).WithParameter("@id", season.Id);
                
                var currentSeasons = await QueryAsync<Season>(queryDef);
                
                foreach (var existing in currentSeasons)
                {
                    existing.IsCurrent = false;
                    await UpsertDocumentAsync(existing, existing.SeasonId);
                }
            }

            // Upsert the target season
            await UpsertDocumentAsync(season, season.SeasonId);
        }
    }
}
