using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core.Domain;
using System.Text.Json;

namespace BowlPoolManager.Api.Functions
{
    public class PoolFunctions
    {
        private readonly ILogger _logger;
        private readonly ICosmosDbService _cosmosService;

        public PoolFunctions(ILoggerFactory loggerFactory, ICosmosDbService cosmosService)
        {
            _logger = loggerFactory.CreateLogger<PoolFunctions>();
            _cosmosService = cosmosService;
        }

        [Function("CreatePool")]
        public async Task<HttpResponseData> CreatePool([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Creating a new pool.");

            var pool = await JsonSerializer.DeserializeAsync<BowlPool>(req.Body);
            
            if (pool == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid pool data.");
                return badResponse;
            }

            await _cosmosService.AddPoolAsync(pool);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(pool);
            return response;
        }

        [Function("GetPools")]
        public async Task<HttpResponseData> GetPools([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Getting all pools.");
            var pools = await _cosmosService.GetPoolsAsync();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(pools);
            return response;
        }
    }
}
