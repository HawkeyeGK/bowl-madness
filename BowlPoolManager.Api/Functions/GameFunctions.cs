using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core;
using System.Text.Json;
using BowlPoolManager.Api.Helpers;
using System.Threading;

namespace BowlPoolManager.Api.Functions
{
    public class GameFunctions
    {
        private readonly ILogger _logger;
        private readonly ICosmosDbService _cosmosService;
        private readonly ICfbdService _cfbdService;

        // STATIC STATE
        private static DateTime _lastRefresh = DateTime.MinValue;
        private static readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);
        private const int RefreshIntervalMinutes = 2; 

        public GameFunctions(ILoggerFactory loggerFactory, ICosmosDbService cosmosService, ICfbdService cfbdService)
        {
            _logger = loggerFactory.CreateLogger<GameFunctions>();
            _cosmosService = cosmosService;
            _cfbdService = cfbdService;
        }

        [Function("GetGames")]
        public async Task<HttpResponseData> GetGames([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var games = await _cosmosService.GetGamesAsync();

            // LAZY LOAD CHECK
            if (DateTime.UtcNow > _lastRefresh.AddMinutes(RefreshIntervalMinutes))
            {
                await _refreshLock.WaitAsync();
                try
                {
                    if (DateTime.UtcNow > _lastRefresh.AddMinutes(RefreshIntervalMinutes))
                    {
                        _logger.LogInformation("Lazy Loading: Refreshing scores from CFBD Scoreboard...");
                        await PerformScoreUpdate(games);
                        _lastRefresh = DateTime.UtcNow;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error refreshing scores.");
                }
                finally
                {
                    _refreshLock.Release();
                }
            }

            var sortedGames = games.OrderBy(g => g.StartTime).ToList();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(sortedGames);
            return response;
        }

        [Function("GetLastScoreUpdate")]
        public async Task<HttpResponseData> GetLastScoreUpdate([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(_lastRefresh);
            return response;
        }

        // --- UPDATED LOGIC USING /SCOREBOARD ---
        private async Task PerformScoreUpdate(List<BowlGame> games)
        {
            var linkedGames = games
                .Where(g => !string.IsNullOrEmpty(g.ExternalId))
                .Where(g => g.Status != GameStatus.Final) 
                .ToList();

            if (!linkedGames.Any()) return;

            // FIX: Use GetScoreboardGamesAsync (Live Data) instead of GetPostseasonGamesAsync
            var apiGames = await _cfbdService.GetScoreboardGamesAsync();
            
            // Fallback: If scoreboard is empty (sometimes happens mid-week), try the games endpoint?
            // For now, let's trust scoreboard for LIVE data.
            
            bool anyChanged = false;

            foreach (var localGame in linkedGames)
            {
                var apiGame = apiGames.FirstOrDefault(x => x.Id.ToString() == localGame.ExternalId);
                if (apiGame == null) continue;

                bool gameChanged = false;

                // --- SAFE UPDATE: Only overwrite if API has a value ---

                // Resolve Home Score
                if (!string.IsNullOrEmpty(localGame.ApiHomeTeam))
                {
                    int? apiScore = null;
                    if (string.Equals(apiGame.HomeTeam, localGame.ApiHomeTeam, StringComparison.OrdinalIgnoreCase))
                        apiScore = apiGame.HomePoints;
                    else if (string.Equals(apiGame.AwayTeam, localGame.ApiHomeTeam, StringComparison.OrdinalIgnoreCase))
                        apiScore = apiGame.AwayPoints;

                    if (apiScore.HasValue && apiScore != localGame.TeamHomeScore)
                    {
                        localGame.TeamHomeScore = apiScore;
                        gameChanged = true;
                    }
                }

                // Resolve Away Score
                if (!string.IsNullOrEmpty(localGame.ApiAwayTeam))
                {
                    int? apiScore = null;
                    if (string.Equals(apiGame.HomeTeam, localGame.ApiAwayTeam, StringComparison.OrdinalIgnoreCase))
                        apiScore = apiGame.HomePoints;
                    else if (string.Equals(apiGame.AwayTeam, localGame.ApiAwayTeam, StringComparison.OrdinalIgnoreCase))
                        apiScore = apiGame.AwayPoints;

                    if (apiScore.HasValue && apiScore != localGame.TeamAwayScore)
                    {
                        localGame.TeamAwayScore = apiScore;
                        gameChanged = true;
                    }
                }

                // Status Logic
                var oldStatus = localGame.Status;
                if (apiGame.Completed)
                {
                    localGame.Status = GameStatus.Final;
                }
                else 
                {
                    if (DateTime.UtcNow >= localGame.StartTime.AddMinutes(-15)) 
                        localGame.Status = GameStatus.InProgress;
                }

                if (localGame.Status != oldStatus) gameChanged = true;

                if (gameChanged)
                {
                    await _cosmosService.UpdateGameAsync(localGame);
                    anyChanged = true;
                }
            }

            if (anyChanged)
            {
                _logger.LogInformation("Scores updated successfully.");
            }
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

            var json = await _cfbdService.GetRawPostseasonGamesJsonAsync(2025);
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
