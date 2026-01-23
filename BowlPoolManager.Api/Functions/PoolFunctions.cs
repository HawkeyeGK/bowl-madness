using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core;
using System.Text.Json;
using BowlPoolManager.Api.Helpers;
using BowlPoolManager.Api.Repositories; // NEW

namespace BowlPoolManager.Api.Functions
{
    public class PoolFunctions
    {
        private readonly ILogger _logger;
        // Replaced ICosmosDbService with specific repos
        private readonly IPoolRepository _poolRepo;
        private readonly IUserRepository _userRepo;

        public PoolFunctions(ILoggerFactory loggerFactory, IPoolRepository poolRepo, IUserRepository userRepo)
        {
            _logger = loggerFactory.CreateLogger<PoolFunctions>();
            _poolRepo = poolRepo;
            _userRepo = userRepo;
        }

        [Function("CreatePool")]
        public async Task<HttpResponseData> CreatePool([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Creating or Updating a pool.");

            try
            {
                // Use SecurityHelper overload with IUserRepository
                var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
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

                if (string.IsNullOrEmpty(pool.Id) && pool.LockDate < DateTime.UtcNow)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Lock Date must be in the future.");
                    return badReq;
                }

                // Use Pool Repo
                await _poolRepo.AddPoolAsync(pool);

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

        [Function("UpdatePool")]
        public async Task<HttpResponseData> UpdatePool([HttpTrigger(AuthorizationLevel.Anonymous, "put")] HttpRequestData req)
        {
            _logger.LogInformation("Updating existing pool.");
            try
            {
                var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
                if (!authResult.IsValid) return authResult.ErrorResponse!;

                var pool = await JsonSerializer.DeserializeAsync<BowlPool>(req.Body);
                if (pool == null || string.IsNullOrEmpty(pool.Id)) return req.CreateResponse(HttpStatusCode.BadRequest);

                if (string.IsNullOrWhiteSpace(pool.Name))
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Pool Name is required.");
                    return badReq;
                }

                // Upsert logic reuse
                await _poolRepo.AddPoolAsync(pool);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(pool);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdatePool failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        [Function("DeletePool")]
        public async Task<HttpResponseData> DeletePool([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "DeletePool/{poolId}")] HttpRequestData req, string poolId)
        {
            _logger.LogInformation($"Deleting pool: {poolId}");
             
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            if (string.IsNullOrEmpty(poolId)) return req.CreateResponse(HttpStatusCode.BadRequest);
            
            await _poolRepo.DeletePoolAsync(poolId);
            return req.CreateResponse(HttpStatusCode.OK);
        }

        [Function("GetPools")]
        public async Task<HttpResponseData> GetPools([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Getting all pools.");
            
            // Use Pool Repo
            var pools = await _poolRepo.GetPoolsAsync();
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(pools);
            return response;
        }

        [Function("ToggleConclusion")]
        public async Task<HttpResponseData> ToggleConclusion(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Pools/{poolId}/ToggleConclusion")] HttpRequestData req,
            string poolId)
        {
            _logger.LogInformation($"Toggling conclusion for pool {poolId}.");
            
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            var pool = await _poolRepo.GetPoolAsync(poolId);
            if (pool == null) return req.CreateResponse(HttpStatusCode.NotFound);

            pool.IsConcluded = !pool.IsConcluded;
            
            await _poolRepo.AddPoolAsync(pool);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(pool);
            return response;
        }
    }
}
