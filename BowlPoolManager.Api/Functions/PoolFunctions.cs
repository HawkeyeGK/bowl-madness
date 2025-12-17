using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core;
using System.Text.Json;
using BowlPoolManager.Api.Helpers;

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
            _logger.LogInformation("Creating or Updating a pool.");

            try
            {
                // Security Check (SuperAdmin Only)
                var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _cosmosService);
                if (!authResult.IsValid) return authResult.ErrorResponse!;

                var pool = await JsonSerializer.DeserializeAsync<BowlPool>(req.Body);
                if (pool == null) return req.CreateResponse(HttpStatusCode.BadRequest);

                // VALIDATION
                if (string.IsNullOrWhiteSpace(pool.Name))
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Pool Name is required.");
                    return badReq;
                }

                if (string.IsNullOrWhiteSpace(pool.InviteCode))
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Invite Code is required.");
                    return badReq;
                }

                // If creating new, ensure lock date is future
                if (string.IsNullOrEmpty(pool.Id) && pool.LockDate < DateTime.UtcNow)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Lock Date must be in the future.");
                    return badReq;
                }

                await _cosmosService.AddPoolAsync(pool);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(pool);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreatePool failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        [Function("GetPools")]
        public async Task<HttpResponseData> GetPools([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Getting all pools.");
            var pools = await _cosmosService.GetPoolsAsync();
            
            // NOTE: In a high-security environment, we might hide InviteCode here 
            // and only show it to Admins. For now, we assume the InviteCode 
            // is semi-public (shared via email).
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(pools);
            return response;
        }
    }
}
