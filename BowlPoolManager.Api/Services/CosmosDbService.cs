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
        // NEW: Get Single Pool (for LockDate checks)
        Task<BowlPool?> GetPoolAsync(string id);

        Task<UserProfile?> GetUserAsync(string id);
        Task UpsertUserAsync(UserProfile user);
        
        Task AddGameAsync(BowlGame game);
        Task UpdateGameAsync(BowlGame game);
        Task<List<BowlGame>> GetGamesAsync();
        
        Task AddEntryAsync(BracketEntry entry);
        Task<List<BracketEntry>> GetEntriesAsync(string? poolId = null);
        Task<BracketEntry?> GetEntryAsync(string id);
        Task DeleteEntryAsync(string id);
        
        // REFACTORED: Support multiple entries per user
        Task<List<BracketEntry>> GetEntriesForUserAsync(string userId, string poolId);
        
        // NEW: Uniqueness Check
        Task<bool> IsBracketNameTakenAsync(string poolId, string bracketName, string? excludeId = null);

        Task<List<UserProfile>> GetUsersAsync();
    }

    public class CosmosDbService : ICosmosDbService
    {
        private readonly Container? _container;

        public CosmosDbService(IConfiguration configuration)
        {
            var connectionString = configuration["CosmosDbConnectionString"];
            
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

        public async Task<BowlPool?> GetPoolAsync(string id) => 
            await GetDocumentAsync<BowlPool>(id);

        public async Task<UserProfile?> GetUserAsync(string id) => 
            await GetDocumentAsync<UserProfile>(id);

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

        public async Task<List<BracketEntry>> GetEntriesAsync(string? poolId = null)
        {
            var sql = $"SELECT * FROM c WHERE c.type = '{Constants.DocumentTypes.BracketEntry}'";
            
            if (!string.IsNullOrEmpty(poolId))
            {
                sql += " AND c.poolId = @poolId";
                var queryDef = new QueryDefinition(sql).WithParameter("@poolId", poolId);
                return await QueryAsync<BracketEntry>(queryDef);
            }

            return await QueryAsync<BracketEntry>(new QueryDefinition(sql));
        }

        public async Task<BracketEntry?> GetEntryAsync(string id) => 
            await GetDocumentAsync<BracketEntry>(id);

        public async Task DeleteEntryAsync(string id)
        {
            if (_container == null) throw new InvalidOperationException("Database connection not initialized.");
            await _container.DeleteItemAsync<BracketEntry>(id, new PartitionKey(id));
        }

        public async Task<List<BracketEntry>> GetEntriesForUserAsync(string userId, string poolId)
        {
            var sql = $"SELECT * FROM c WHERE c.type = '{Constants.DocumentTypes.BracketEntry}' AND c.userId = @userId AND c.poolId = @poolId";
            var queryDef = new QueryDefinition(sql)
                .WithParameter("@userId", userId)
                .WithParameter("@poolId", poolId);

            return await QueryAsync<BracketEntry>(queryDef);
        }

        public async Task<bool> IsBracketNameTakenAsync(string poolId, string bracketName, string? excludeId = null)
        {
            if (_container == null) return false;

            // Case-insensitive check for name uniqueness within a pool
            var sql = $"SELECT VALUE COUNT(1) FROM c WHERE c.type = '{Constants.DocumentTypes.BracketEntry}' " +
                      "AND c.poolId = @poolId AND StringEquals(c.playerName, @name, true)";

            var queryDef = new QueryDefinition(sql)
                .WithParameter("@poolId", poolId)
                .WithParameter("@name", bracketName);

            if (!string.IsNullOrEmpty(excludeId))
            {
                sql += " AND c.id != @excludeId";
                queryDef = new QueryDefinition(sql)
                    .WithParameter("@poolId", poolId)
                    .WithParameter("@name", bracketName)
                    .WithParameter("@excludeId", excludeId);
            }

            var iterator = _container.GetItemQueryIterator<int>(queryDef);
            if (iterator.HasMoreResults)
            {
                var result = await iterator.ReadNextAsync();
                return result.FirstOrDefault() > 0;
            }

            return false;
        }

        public async Task<List<UserProfile>> GetUsersAsync() => 
            await GetListAsync<UserProfile>(Constants.DocumentTypes.UserProfile);

        // --- INTERNAL GENERIC HELPERS ---

        private async Task UpsertDocumentAsync<T>(T item, string id)
        {
            if (_container == null) throw new InvalidOperationException("Database connection not initialized.");
            await _container.UpsertItemAsync(item, new PartitionKey(id));
        }

        private async Task<T?> GetDocumentAsync<T>(string id)
        {
            if (_container == null) return default;
            try
            {
                ItemResponse<T> response = await _container.ReadItemAsync<T>(id, new PartitionKey(id));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return default;
            }
        }

        private async Task<List<T>> GetListAsync<T>(string documentType)
        {
            var sql = $"SELECT * FROM c WHERE c.type = '{documentType}'";
            return await QueryAsync<T>(new QueryDefinition(sql));
        }

        // Updated to accept QueryDefinition for parameters
        private async Task<List<T>> QueryAsync<T>(QueryDefinition queryDef)
        {
            if (_container == null) throw new InvalidOperationException("Database connection not initialized.");

            var query = _container.GetItemQueryIterator<T>(queryDef);
            var results = new List<T>();

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                results.AddRange(response.ToList());
            }

            return results;
        }

        // Backwards compatibility overload (if needed by other code not yet updated)
        private async Task<List<T>> QueryAsync<T>(string sqlQuery)
        {
            return await QueryAsync<T>(new QueryDefinition(sqlQuery));
        }
    }
}
