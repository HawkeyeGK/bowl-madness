using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Api.Helpers;
using System.Text.Json;
using BowlPoolManager.Api.Repositories; // NEW

namespace BowlPoolManager.Api.Functions
{
    public class GameFunctions
    {
        private readonly ILogger _logger;
        // REMOVED: private readonly ICosmosDbService _cosmosService;
        // ADDED Repositories:
        private readonly IGameRepository _gameRepo;
        private readonly IUserRepository _userRepo;
        
        private readonly ICfbdService _cfbdService;
        private readonly IGameScoringService _scoringService;

        public GameFunctions(ILoggerFactory loggerFactory, 
                             IGameRepository gameRepo, // Injected
                             IUserRepository userRepo, // Injected
                             ICfbdService cfbdService,
                             IGameScoringService scoringService)
        {
            _logger = loggerFactory.CreateLogger<GameFunctions>();
            _gameRepo = gameRepo;
            _userRepo = userRepo;
            _cfbdService = cfbdService;
            _scoringService = scoringService;
        }

        [Function("GetGames")]
        public async Task<HttpResponseData> GetGames([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            // Use Repo
            var games = await _gameRepo.GetGamesAsync();

            // Delegate logic to the service
            await _scoringService.CheckAndRefreshScoresAsync(games);

            var sortedGames = games.OrderBy(g => g.StartTime).ToList();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(sortedGames);
            return response;
        }

        [Function("GetLastScoreUpdate")]
        public async Task<HttpResponseData> GetLastScoreUpdate([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var lastUpdate = _scoringService.GetLastRefreshTime();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(lastUpdate);
            return response;
        }

        [Function("GetExternalGames")]
        public async Task<HttpResponseData> GetExternalGames([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            // Use UserRepo overload
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            var games = await _cfbdService.GetPostseasonGamesAsync(2025);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(games);
            return response;
        }
        
        [Function("GetRawCfbdGames")]
        public async Task<HttpResponseData> GetRawCfbdGames([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            // Use UserRepo overload
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            var source = req.Query["source"];
            string json;
            
            if (source == "scoreboard")
            {
                // Fix: Use the Typed service method to ensure our manual mapping (Notes, etc.) is applied,
                // then re-serialize to send to client.
                var games = await _cfbdService.GetScoreboardGamesAsync();
                json = JsonSerializer.Serialize(games);
            }
            else
            {
                json = await _cfbdService.GetRawPostseasonGamesJsonAsync(2025);
            }
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(json);
            return response;
        }

        [Function("SaveGame")]
        public async Task<HttpResponseData> SaveGame([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
             // Use UserRepo overload
             var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
             if (!authResult.IsValid) return authResult.ErrorResponse!;

             var game = await JsonSerializer.DeserializeAsync<BowlGame>(req.Body);
             if (game != null) await _gameRepo.UpdateGameAsync(game); // Use Repo
             
             return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
