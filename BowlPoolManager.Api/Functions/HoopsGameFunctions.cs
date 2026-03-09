using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;
using BowlPoolManager.Api.Helpers;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Api.Services;

namespace BowlPoolManager.Api.Functions
{
    public class HoopsGameFunctions
    {
        private readonly ILogger _logger;
        private readonly IHoopsGameRepository _gameRepo;
        private readonly IHoopsPoolRepository _poolRepo;
        private readonly IUserRepository _userRepo;
        private readonly IBracketGeneratorService _bracketGenerator;
        private readonly IHoopsGameScoringService _scoringService;
        private readonly IEspnDataService _espnService;

        public HoopsGameFunctions(
            ILoggerFactory loggerFactory,
            IHoopsGameRepository gameRepo,
            IHoopsPoolRepository poolRepo,
            IUserRepository userRepo,
            IBracketGeneratorService bracketGenerator,
            IHoopsGameScoringService scoringService,
            IEspnDataService espnService)
        {
            _logger = loggerFactory.CreateLogger<HoopsGameFunctions>();
            _gameRepo = gameRepo;
            _poolRepo = poolRepo;
            _userRepo = userRepo;
            _bracketGenerator = bracketGenerator;
            _scoringService = scoringService;
            _espnService = espnService;
        }

        [Function("GetHoopsGames")]
        public async Task<HttpResponseData> GetHoopsGames(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var poolId = req.Query["poolId"];
            if (string.IsNullOrEmpty(poolId)) return req.CreateResponse(HttpStatusCode.BadRequest);

            var pool = await _poolRepo.GetPoolAsync(poolId);
            if (pool == null) return req.CreateResponse(HttpStatusCode.NotFound);

            var allGames = await _gameRepo.GetGamesAsync(pool.SeasonId);
            var poolGameIds = pool.GameIds.ToHashSet();
            var games = allGames.Where(g => poolGameIds.Contains(g.Id)).ToList();

            // Lazy-load live score refresh (throttled to once every 2 minutes)
            await _scoringService.CheckAndRefreshScoresAsync(games);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(games);
            return response;
        }

        [Function("GetRawHoopsGames")]
        public async Task<HttpResponseData> GetRawHoopsGames(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            try
            {
                var games = await _espnService.GetScoreboardGamesAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(games);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetRawHoopsGames failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        [Function("GenerateBracket")]
        public async Task<HttpResponseData> GenerateBracket(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            try
            {
                var request = await JsonSerializer.DeserializeAsync<BracketGenerationRequest>(req.Body);
                if (request == null ||
                    string.IsNullOrEmpty(request.PoolId) ||
                    string.IsNullOrEmpty(request.SeasonId))
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

                var pool = await _poolRepo.GetPoolAsync(request.PoolId);
                if (pool == null) return req.CreateResponse(HttpStatusCode.NotFound);

                // Delete existing bracket games if the pool already has one.
                // Note: GetGamesAsync fetches all HoopsGame docs for the season; we filter to
                // this pool's GameIds client-side. If multiple pools share a season, the over-read
                // is harmless (filter prevents incorrect deletion) but acceptable for this admin-only flow.
                if (pool.GameIds.Any())
                {
                    var existing = await _gameRepo.GetGamesAsync(pool.SeasonId);
                    var poolGameIds = pool.GameIds.ToHashSet();
                    var toDelete = existing.Where(g => poolGameIds.Contains(g.Id)).ToList();
                    if (toDelete.Any())
                        await _gameRepo.DeleteGamesAsBatchAsync(toDelete, pool.SeasonId);
                }

                // Generate and persist the new bracket
                var games = _bracketGenerator.GenerateBracket(request);
                await _gameRepo.SaveGamesAsBatchAsync(games, request.SeasonId);

                // Update the pool with the new game IDs
                pool.GameIds = games.Select(g => g.Id).ToList();
                await _poolRepo.AddPoolAsync(pool);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(games);
                return response;
            }
            catch (ArgumentException ex)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync(ex.Message);
                return bad;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GenerateBracket failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        [Function("UpdateHoopsGame")]
        public async Task<HttpResponseData> UpdateHoopsGame(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put")] HttpRequestData req)
        {
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            try
            {
                var game = await JsonSerializer.DeserializeAsync<HoopsGame>(req.Body);
                if (game == null ||
                    string.IsNullOrEmpty(game.Id) ||
                    string.IsNullOrEmpty(game.SeasonId))
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

                await _gameRepo.UpdateGameAsync(game);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(game);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateHoopsGame failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        [Function("SaveHoopsTeamAssignments")]
        public async Task<HttpResponseData> SaveHoopsTeamAssignments(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            try
            {
                var games = await JsonSerializer.DeserializeAsync<List<HoopsGame>>(req.Body);
                if (games == null || !games.Any()) return req.CreateResponse(HttpStatusCode.BadRequest);

                var seasonId = games.First().SeasonId;
                if (string.IsNullOrEmpty(seasonId) || games.Any(g => g.SeasonId != seasonId))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("All games must have the same SeasonId.");
                    return bad;
                }

                await _gameRepo.SaveGamesAsBatchAsync(games, seasonId);

                return req.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveHoopsTeamAssignments failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        [Function("DeleteHoopsGame")]
        public async Task<HttpResponseData> DeleteHoopsGame(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "DeleteHoopsGame/{gameId}")] HttpRequestData req,
            string gameId)
        {
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            if (string.IsNullOrEmpty(gameId)) return req.CreateResponse(HttpStatusCode.BadRequest);

            var seasonId = req.Query["seasonId"];
            if (string.IsNullOrEmpty(seasonId)) return req.CreateResponse(HttpStatusCode.BadRequest);

            await _gameRepo.DeleteGameAsync(gameId, seasonId);
            return req.CreateResponse(HttpStatusCode.OK);
        }

        [Function("SaveHoopsGame")]
        public async Task<HttpResponseData> SaveHoopsGame(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            try
            {
                var game = await JsonSerializer.DeserializeAsync<HoopsGame>(req.Body);
                if (game == null ||
                    string.IsNullOrEmpty(game.Id) ||
                    string.IsNullOrEmpty(game.SeasonId))
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

                await _scoringService.ProcessGameUpdateAsync(game);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(game);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveHoopsGame failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        [Function("ForceHoopsPropagation")]
        public async Task<HttpResponseData> ForceHoopsPropagation(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            var seasonId = req.Query["seasonId"];
            if (string.IsNullOrEmpty(seasonId)) return req.CreateResponse(HttpStatusCode.BadRequest);

            try
            {
                await _scoringService.ForcePropagateAllAsync(seasonId);
                return req.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ForceHoopsPropagation failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
