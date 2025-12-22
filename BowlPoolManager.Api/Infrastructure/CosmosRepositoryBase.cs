using Microsoft.Azure.Cosmos;
using BowlPoolManager.Core;

namespace BowlPoolManager.Api.Infrastructure
{
    public abstract class CosmosRepositoryBase
    {
        protected readonly Container _container;

        protected CosmosRepositoryBase(Container container)
        {
            _container = container;
        }

        protected async Task UpsertDocumentAsync<T>(T item, string id)
        {
            await _container.UpsertItemAsync(item, new PartitionKey(id));
        }

        protected async Task<T?> GetDocumentAsync<T>(string id)
        {
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

        protected async Task<List<T>> GetListAsync<T>(string documentType)
        {
            var sql = $"SELECT * FROM c WHERE c.type = '{documentType}'";
            return await QueryAsync<T>(new QueryDefinition(sql));
        }

        protected async Task<List<T>> QueryAsync<T>(QueryDefinition queryDef)
        {
            var query = _container.GetItemQueryIterator<T>(queryDef);
            var results = new List<T>();

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                results.AddRange(response.ToList());
            }

            return results;
        }

        protected async Task DeleteDocumentAsync<T>(string id)
        {
             await _container.DeleteItemAsync<T>(id, new PartitionKey(id));
        }
    }
}
