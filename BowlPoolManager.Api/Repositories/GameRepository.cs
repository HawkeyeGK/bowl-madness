using Microsoft.Azure.Cosmos;
using BowlPoolManager.Api.Infrastructure;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Repositories
{
    public class GameRepository : CosmosRepositoryBase, IGameRepository
    {
        public GameRepository(CosmosClient cosmosClient) : base(cosmosClient, Constants.Database.SeasonsContainer) { }

        public async Task AddGameAsync(BowlGame game) => await UpsertDocumentAsync(game, game.SeasonId);
        public async Task UpdateGameAsync(BowlGame game) => await UpsertDocumentAsync(game, game.SeasonId);
        public async Task<List<BowlGame>> GetGamesAsync(string? seasonId = null) 
        {
            if (!string.IsNullOrEmpty(seasonId))
            {
                var sql = "SELECT * FROM c WHERE c.type = 'BowlGame'";
                return await QueryAsync<BowlGame>(new QueryDefinition(sql), seasonId);
            }
            return await GetListAsync<BowlGame>(Constants.DocumentTypes.BowlGame);
        }
        
        public async Task DeleteGameAsync(string gameId, string seasonId)
        {
             await DeleteDocumentAsync<BowlGame>(gameId, seasonId);
        }
    }
}
