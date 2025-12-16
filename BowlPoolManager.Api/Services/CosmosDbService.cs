using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core;

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
        Task AddEntryAsync(BracketEntry entry);
        Task<List<BracketEntry>> GetEntriesAsync();
        Task<BracketEntry?> GetEntryAsync(string id);

        // NEW: Get All Users
        Task<List<UserProfile>> GetUsersAsync();

        // NEW: Delete Entry
        Task DeleteEntryAsync(string id);
    }

    public class CosmosDbService : ICosmosDbService
    {
        private readonly Container? _container;

        public CosmosDbService(IConfiguration configuration)
        {
            var connectionString = configuration["CosmosDbConnectionString"];
            
            // Graceful fallback for build environments
            if (string.IsNullOrEmpty(connectionString)) 
            {
                 _container = null;
                 return;
            }

            var client = new CosmosClient(connectionString);
            var database = client.GetDatabase(Constants.Database.DbName);
            _container = database.GetContainer(Constants.Database.ContainerName);
        }

        // --- PUBLIC INTERFACE IMPLEMENTATION ---

        public async Task AddPoolAsync(BowlPool pool) => 
            await UpsertDocumentAsync(pool, pool.Id);

        public async Task<List<BowlPool>> GetPoolsAsync() => 
            await GetListAsync<BowlPool>(Constants.DocumentTypes.BowlPool);

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

        public async Task UpsertUserAsync(UserProfile user) => 
            await UpsertDocumentAsync(user, user.Id);

        public async Task AddGameAsync(BowlGame game) => 
            await UpsertDocumentAsync(game, game.Id);

        public async Task UpdateGameAsync(BowlGame game) => 
            await UpsertDocumentAsync(game, game.Id);

        public async Task<List<BowlGame>> GetGamesAsync() => 
            await GetListAsync<BowlGame>(Constants.DocumentTypes.BowlGame);

        public async Task AddEntryAsync(BracketEntry entry) => 
            await UpsertDocumentAsync(entry, entry.Id);

        public async Task<List<BracketEntry>> GetEntriesAsync() => 
            await GetListAsync<BracketEntry>(Constants.DocumentTypes.BracketEntry);

        public async Task<BracketEntry?> GetEntryAsync(string id)
        {
            if (_container == null) return null;
            try
            {
                ItemResponse<BracketEntry> response = await _container.ReadItemAsync<BracketEntry>(id, new PartitionKey(id));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        // NEW: Get All Users Implementation
        public async Task<List<UserProfile>> GetUsersAsync() => 
            await GetListAsync<UserProfile>(Constants.DocumentTypes.UserProfile);

        // NEW: Delete Implementation
        public async Task DeleteEntryAsync(string id)
        {
            if (_container == null) throw new InvalidOperationException("Database connection not initialized.");
            // PartitionKey is /id
            await _container.DeleteItemAsync<BracketEntry>(id, new PartitionKey(id));
        }

        // --- INTERNAL GENERIC HELPERS ---

        private async Task UpsertDocumentAsync<T>(T item, string id)
        {
            if (_container == null) throw new InvalidOperationException("Database connection not initialized.");
            await _container.UpsertItemAsync(item, new PartitionKey(id));
        }

        private async Task<List<T>> GetListAsync<T>(string documentType)
        {
            // Simple generic query based on the 'type' discriminator
            var sql = $"SELECT * FROM c WHERE c.type = '{documentType}'";
            return await QueryAsync<T>(sql);
        }

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
