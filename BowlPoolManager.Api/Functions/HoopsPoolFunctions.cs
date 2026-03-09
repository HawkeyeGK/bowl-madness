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
        private readonly IHoopsGameRepository _gameRepo;
        private readonly IHoopsEntryRepository _entryRepo;
        private readonly IUserRepository _userRepo;

        public HoopsPoolFunctions(
            ILoggerFactory loggerFactory,
            IHoopsPoolRepository poolRepo,
            IHoopsGameRepository gameRepo,
            IHoopsEntryRepository entryRepo,
            IUserRepository userRepo)
        {
            _logger = loggerFactory.CreateLogger<HoopsPoolFunctions>();
            _poolRepo = poolRepo;
            _gameRepo = gameRepo;
            _entryRepo = entryRepo;
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

            var pool = await _poolRepo.GetPoolAsync(poolId);
            if (pool == null) return req.CreateResponse(HttpStatusCode.NotFound);

            // Delete bracket games
            if (pool.GameIds.Any())
            {
                var allGames = await _gameRepo.GetGamesAsync(pool.SeasonId);
                var poolGameIds = pool.GameIds.ToHashSet();
                var gamesToDelete = allGames.Where(g => poolGameIds.Contains(g.Id)).ToList();
                if (gamesToDelete.Any())
                {
                    await _gameRepo.DeleteGamesAsBatchAsync(gamesToDelete, pool.SeasonId);
                    _logger.LogInformation("DeleteHoopsPool: Deleted {Count} games for pool '{PoolId}'.", gamesToDelete.Count, poolId);
                }
            }

            // Delete entries
            var entries = await _entryRepo.GetEntriesAsync(poolId: poolId);
            foreach (var entry in entries)
                await _entryRepo.DeleteEntryAsync(entry.Id, entry.SeasonId);
            if (entries.Any())
                _logger.LogInformation("DeleteHoopsPool: Deleted {Count} entries for pool '{PoolId}'.", entries.Count, poolId);

            // Delete the pool itself
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
