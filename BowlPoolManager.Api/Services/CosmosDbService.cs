using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Services
{
    public interface ICosmosDbService
    {
        Task AddPoolAsync(BowlPool pool);
        Task<List<BowlPool>> GetPoolsAsync();
        Task<UserProfile?> GetUserAsync(string id);
        Task UpsertUserAsync(UserProfile user);
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
            var database = client.GetDatabase("BowlMadnessDb");
            _container = database.GetContainer("MainContainer");
        }

        public async Task AddPoolAsync(BowlPool pool)
        {
            if (_container == null) throw new InvalidOperationException("Database connection not initialized.");
            await _container.CreateItemAsync(pool, new PartitionKey(pool.Id));
        }

        public async Task<List<BowlPool>> GetPoolsAsync()
        {
            if (_container == null) throw new InvalidOperationException("Database connection not initialized.");
            
            var query = _container.GetItemQueryIterator<BowlPool>(new QueryDefinition("SELECT * FROM c WHERE c.type = 'BowlPool'"));
            var results = new List<BowlPool>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                results.AddRange(response.ToList());
            }
            return results;
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
    }
}
