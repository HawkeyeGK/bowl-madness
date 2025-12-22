using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Api.Helpers;
using System.Text.Json;

namespace BowlPoolManager.Api.Functions
{
    public class GameFunctions
    {
        private readonly ILogger _logger;
        private readonly ICosmosDbService _cosmosService;
        private readonly ICfbdService _cfbdService;
        private readonly IGameScoringService _scoringService;

        public GameFunctions(ILoggerFactory loggerFactory, 
                             ICosmosDbService cosmosService, 
                             ICfbdService cfbdService,
                             IGameScoringService scoringService)
        {
            _logger = loggerFactory.CreateLogger<GameFunctions>();
            _cosmosService = cosmosService;
            _cfbdService = cfbdService;
            _scoringService = scoringService;
        }

        [Function("GetGames")]
        public async Task<HttpResponseData> GetGames([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var games = await _cosmosService.GetGamesAsync();

            // Delegate logic to the service
            await _scoringService.CheckAndRefreshScoresAsync(games);

            var sortedGames = games.OrderBy(g => g.StartTime).ToList();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(sortedGames);
            return response;
        }

        [Function("GetExternalGames")]
        public async Task<HttpResponseData> GetExternalGames([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _cosmosService);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            var games = await _cfbdService.GetPostseasonGamesAsync(2025);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(games);
            return response;
        }
        
        [Function("GetRawCfbdGames")]
        public async Task<HttpResponseData> GetRawCfbdGames([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _cosmosService);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            var source = req.Query["source"];
            string json;
            
            if (source == "scoreboard")
            {
                json = await _cfbdService.GetRawScoreboardJsonAsync();
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
             var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _cosmosService);
             if (!authResult.IsValid) return authResult.ErrorResponse!;

             var game = await JsonSerializer.DeserializeAsync<BowlGame>(req.Body);
             if (game != null) await _cosmosService.UpdateGameAsync(game);
             
             return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
