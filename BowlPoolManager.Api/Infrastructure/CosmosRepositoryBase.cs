using Microsoft.Azure.Cosmos;
using BowlPoolManager.Core;

namespace BowlPoolManager.Api.Infrastructure
{
    public abstract class CosmosRepositoryBase
    {
        protected readonly Container _container;

        // Changed: Inject Client and Container Name, not a pre-built Container
        protected CosmosRepositoryBase(CosmosClient cosmosClient, string containerName, string databaseName = Constants.Database.DbName)
        {
            _container = cosmosClient.GetContainer(databaseName, containerName);
        }

        protected async Task UpsertDocumentAsync<T>(T item, string partitionKey)
        {
            await _container.UpsertItemAsync(item, new PartitionKey(partitionKey));
        }

        protected async Task<T?> GetDocumentAsync<T>(string id, string partitionKey)
        {
            try
            {
                ItemResponse<T> response = await _container.ReadItemAsync<T>(id, new PartitionKey(partitionKey));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return default;
            }
        }

        protected async Task<List<T>> GetListAsync<T>(string documentType)
        {
            var sql = $"SELECT * FROM c WHERE c.type = '{documentType}'";
            return await QueryAsync<T>(new QueryDefinition(sql));
        }

        protected async Task<List<T>> QueryAsync<T>(QueryDefinition queryDef, string? partitionKey = null)
        {
            var requestOptions = new QueryRequestOptions();
            if(!string.IsNullOrEmpty(partitionKey))
            {
                requestOptions.PartitionKey = new PartitionKey(partitionKey);
            }

            var query = _container.GetItemQueryIterator<T>(queryDef, requestOptions: requestOptions);
            var results = new List<T>();

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                results.AddRange(response.ToList());
            }

            return results;
        }

        protected async Task DeleteDocumentAsync<T>(string id, string partitionKey)
        {
             await _container.DeleteItemAsync<T>(id, new PartitionKey(partitionKey));
        }
    }
}
