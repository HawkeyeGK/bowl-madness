using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core; // Added reference

namespace BowlPoolManager.Api.Services
{
    public interface ICosmosDbService
    {
        Task AddPoolAsync(BowlPool pool);
        Task<List<BowlPool>> GetPoolsAsync();
        Task<UserProfile?> GetUserAsync(string id);
        Task UpsertUserAsync(UserProfile user);
        Task AddGameAsync(BowlGame game);
        Task UpdateGameAsync(BowlGame game);
        Task<List<BowlGame>> GetGamesAsync();
    }

    public class CosmosDbService : ICosmosDbService
    {
        private readonly Container? _container;

        public CosmosDbService(IConfiguration configuration)
        {
            var connectionString = configuration["CosmosDbConnectionString"];
            
            // Graceful fallback for build environments (GitHub Actions)
            if (string.IsNullOrEmpty(connectionString)) 
            {
                 _container = null;
                 return;
            }

            var client = new CosmosClient(connectionString);
            // UPDATED: Using Constants
            var database = client.GetDatabase(Constants.Database.DbName);
            _container = database.GetContainer(Constants.Database.ContainerName);
        }

        public async Task AddPoolAsync(BowlPool pool)
        {
            if (_container == null) throw new InvalidOperationException("Database connection not initialized.");
            await _container.CreateItemAsync(pool, new PartitionKey(pool.Id));
        }

        public async Task<List<BowlPool>> GetPoolsAsync()
        {
            var sql = $"SELECT * FROM c WHERE c.type = '{Constants.DocumentTypes.BowlPool}'";
            return await QueryAsync<BowlPool>(sql);
        }

        public async Task<UserProfile?> GetUserAsync(string id)
        {
            if (_container == null) return null;
            try
            {
                ItemResponse<UserProfile> response = await _container.ReadItemAsync<UserProfile>(id, new PartitionKey(id));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task UpsertUserAsync(UserProfile user)
        {
            if (_container == null) throw new InvalidOperationException("Database connection not initialized.");
            await _container.UpsertItemAsync(user, new PartitionKey(user.Id));
        }

        public async Task AddGameAsync(BowlGame game)
        {
            if (_container == null) throw new InvalidOperationException("Database connection not initialized.");
            await _container.CreateItemAsync(game, new PartitionKey(game.Id));
        }

        public async Task UpdateGameAsync(BowlGame game)
        {
            if (_container == null) throw new InvalidOperationException("Database connection not initialized.");
            await _container.UpsertItemAsync(game, new PartitionKey(game.Id));
        }

        public async Task<List<BowlGame>> GetGamesAsync()
        {
            var sql = $"SELECT * FROM c WHERE c.type = '{Constants.DocumentTypes.BowlGame}'";
            return await QueryAsync<BowlGame>(sql);
        }

        // Generic Helper for all future list queries
        private async Task<List<T>> QueryAsync<T>(string sqlQuery)
        {
            if (_container == null) throw new InvalidOperationException("Database connection not initialized.");

            var query = _container.GetItemQueryIterator<T>(new QueryDefinition(sqlQuery));
            var results = new List<T>();

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                results.AddRange(response.ToList());
            }

            return results;
        }
    }
}
