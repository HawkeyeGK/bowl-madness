using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Api.Helpers;
using BowlPoolManager.Api.Repositories;

namespace BowlPoolManager.Api.Functions
{
    public class HoopsPoolFunctions
    {
        private readonly ILogger _logger;
        private readonly IHoopsPoolRepository _poolRepo;
        private readonly IUserRepository _userRepo;

        public HoopsPoolFunctions(ILoggerFactory loggerFactory, IHoopsPoolRepository poolRepo, IUserRepository userRepo)
        {
            _logger = loggerFactory.CreateLogger<HoopsPoolFunctions>();
            _poolRepo = poolRepo;
            _userRepo = userRepo;
        }

        [Function("CreateHoopsPool")]
        public async Task<HttpResponseData> CreateHoopsPool([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            try
            {
                var pool = await JsonSerializer.DeserializeAsync<HoopsPool>(req.Body);
                if (pool == null) return req.CreateResponse(HttpStatusCode.BadRequest);

                if (string.IsNullOrWhiteSpace(pool.Name))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Pool Name is required.");
                    return bad;
                }

                if (string.IsNullOrWhiteSpace(pool.InviteCode))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Invite Code is required.");
                    return bad;
                }

                if (string.IsNullOrEmpty(pool.Id) && pool.LockDate < DateTime.UtcNow)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Lock Date must be in the future.");
                    return bad;
                }

                await _poolRepo.AddPoolAsync(pool);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(pool);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateHoopsPool failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        [Function("UpdateHoopsPool")]
        public async Task<HttpResponseData> UpdateHoopsPool([HttpTrigger(AuthorizationLevel.Anonymous, "put")] HttpRequestData req)
        {
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            try
            {
                var pool = await JsonSerializer.DeserializeAsync<HoopsPool>(req.Body);
                if (pool == null || string.IsNullOrEmpty(pool.Id)) return req.CreateResponse(HttpStatusCode.BadRequest);

                if (string.IsNullOrWhiteSpace(pool.Name))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Pool Name is required.");
                    return bad;
                }

                await _poolRepo.AddPoolAsync(pool);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(pool);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateHoopsPool failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        [Function("DeleteHoopsPool")]
        public async Task<HttpResponseData> DeleteHoopsPool(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "DeleteHoopsPool/{poolId}")] HttpRequestData req,
            string poolId)
        {
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            if (string.IsNullOrEmpty(poolId)) return req.CreateResponse(HttpStatusCode.BadRequest);

            await _poolRepo.DeletePoolAsync(poolId);
            return req.CreateResponse(HttpStatusCode.OK);
        }

        [Function("GetHoopsPools")]
        public async Task<HttpResponseData> GetHoopsPools([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var seasonId = req.Query["seasonId"];
            var pools = await _poolRepo.GetPoolsAsync(seasonId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(pools);
            return response;
        }

        [Function("ToggleHoopsConclusion")]
        public async Task<HttpResponseData> ToggleHoopsConclusion(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "HoopsPools/{poolId}/ToggleConclusion")] HttpRequestData req,
            string poolId)
        {
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
