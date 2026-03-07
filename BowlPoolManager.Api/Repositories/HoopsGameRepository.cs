using Microsoft.Azure.Cosmos;
using BowlPoolManager.Api.Infrastructure;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Repositories
{
    public class HoopsGameRepository : CosmosRepositoryBase, IHoopsGameRepository
    {
        public HoopsGameRepository(CosmosClient cosmosClient)
            : base(cosmosClient, Constants.Database.SeasonsContainer) { }

        public async Task AddGameAsync(HoopsGame game) =>
            await UpsertDocumentAsync(game, game.SeasonId);

        public async Task UpdateGameAsync(HoopsGame game) =>
            await UpsertDocumentAsync(game, game.SeasonId);

        public async Task<List<HoopsGame>> GetGamesAsync(string seasonId)
        {
            var sql = $"SELECT * FROM c WHERE c.type = '{Constants.DocumentTypes.HoopsGame}'";
            return await QueryAsync<HoopsGame>(new QueryDefinition(sql), seasonId);
        }

        public async Task DeleteGameAsync(string gameId, string seasonId) =>
            await DeleteDocumentAsync<HoopsGame>(gameId, seasonId);

        public async Task SaveGamesAsBatchAsync(List<HoopsGame> games, string seasonId)
        {
            if (games == null || !games.Any()) return;
            if (games.Count > 100)
                throw new ArgumentException($"Batch size {games.Count} exceeds the Cosmos DB transactional batch limit of 100 items.");

            var batch = _container.CreateTransactionalBatch(new PartitionKey(seasonId));
            foreach (var game in games)
                batch.UpsertItem(game);

            using var response = await batch.ExecuteAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Batch save failed with status {response.StatusCode}: {response.ErrorMessage}");
        }

        public async Task DeleteGamesAsBatchAsync(List<HoopsGame> games, string seasonId)
        {
            if (games == null || !games.Any()) return;

            var batch = _container.CreateTransactionalBatch(new PartitionKey(seasonId));
            foreach (var game in games)
                batch.DeleteItem(game.Id);

            using var response = await batch.ExecuteAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Batch delete failed with status {response.StatusCode}: {response.ErrorMessage}");
        }
    }
}
