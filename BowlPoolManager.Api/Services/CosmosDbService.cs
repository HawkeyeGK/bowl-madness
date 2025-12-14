using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Services
{
    public interface ICosmosDbService
    {
        Task AddPoolAsync(BowlPool pool);
        Task<List<BowlPool>> GetPoolsAsync();
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
    }
}
