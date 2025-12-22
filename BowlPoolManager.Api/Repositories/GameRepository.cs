using Microsoft.Azure.Cosmos;
using BowlPoolManager.Api.Infrastructure;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Repositories
{
    public class GameRepository : CosmosRepositoryBase, IGameRepository
    {
        public GameRepository(Container container) : base(container) { }

        public async Task AddGameAsync(BowlGame game) => await UpsertDocumentAsync(game, game.Id);
        public async Task UpdateGameAsync(BowlGame game) => await UpsertDocumentAsync(game, game.Id);
        public async Task<List<BowlGame>> GetGamesAsync() => await GetListAsync<BowlGame>(Constants.DocumentTypes.BowlGame);
    }
}
