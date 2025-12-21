using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core;
using System.Text.Json;
using BowlPoolManager.Api.Helpers;
using System.Threading; // FIXED: Required for SemaphoreSlim

namespace BowlPoolManager.Api.Functions
{
    public class GameFunctions
    {
        private readonly ILogger _logger;
        private readonly ICosmosDbService _cosmosService;
        private readonly ICfbdService _cfbdService;

        // STATIC STATE: Persists between function invocations
        // This prevents spamming the API. We only check if it's been > 2 minutes.
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
            // 1. Fetch Current Data
            var games = await _cosmosService.GetGamesAsync();

            // 2. LAZY LOAD CHECK: Is it time to refresh scores?
            if (DateTime.UtcNow > _lastRefresh.AddMinutes(RefreshIntervalMinutes))
            {
                // Ensure only one user triggers the update at a time
                await _refreshLock.WaitAsync();
                try
                {
                    // Double-check timestamp inside the lock
                    if (DateTime.UtcNow > _lastRefresh.AddMinutes(RefreshIntervalMinutes))
                    {
                        _logger.LogInformation("Lazy Loading: Refreshing scores from CFBD...");
                        await PerformScoreUpdate(games);
                        _lastRefresh = DateTime.UtcNow;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error refreshing scores.");
                    // Fail silently so the user still gets the cached data
                }
                finally
                {
                    _refreshLock.Release();
                }
            }

            // 3. Return Data
            var sortedGames = games.OrderBy(g => g.StartTime).ToList();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(sortedGames);
            return response;
        }

        // --- NEW ENDPOINT: Returns the last update time ---
        [Function("GetLastScoreUpdate")]
        public async Task<HttpResponseData> GetLastScoreUpdate([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            // Return as JSON string so Client can parse as DateTime
            await response.WriteAsJsonAsync(_lastRefresh);
            return response;
        }

        // --- BRIDGE UPDATE LOGIC ---
        private async Task PerformScoreUpdate(List<BowlGame> games)
        {
            var linkedGames = games
                .Where(g => !string.IsNullOrEmpty(g.ExternalId))
                .Where(g => g.Status != GameStatus.Final) 
                .ToList();

            if (!linkedGames.Any()) return;

            // Fetch 2025 Data
            var apiGames = await _cfbdService.GetPostseasonGamesAsync(2025);
            
            bool anyChanged = false;

            foreach (var localGame in linkedGames)
            {
                var apiGame = apiGames.FirstOrDefault(x => x.Id.ToString() == localGame.ExternalId);
                if (apiGame == null) continue;

                bool gameChanged = false;

                // Resolve Local Home Score
                if (!string.IsNullOrEmpty(localGame.ApiHomeTeam))
                {
                    int? newScore = null;
                    if (string.Equals(apiGame.HomeTeam, localGame.ApiHomeTeam, StringComparison.OrdinalIgnoreCase))
                        newScore = apiGame.HomePoints;
                    else if (string.Equals(apiGame.AwayTeam, localGame.ApiHomeTeam, StringComparison.OrdinalIgnoreCase))
                        newScore = apiGame.AwayPoints;

                    if (newScore != localGame.TeamHomeScore)
                    {
                        localGame.TeamHomeScore = newScore;
                        gameChanged = true;
                    }
                }

                // Resolve Local Away Score
                if (!string.IsNullOrEmpty(localGame.ApiAwayTeam))
                {
                    int? newScore = null;
                    if (string.Equals(apiGame.HomeTeam, localGame.ApiAwayTeam, StringComparison.OrdinalIgnoreCase))
                        newScore = apiGame.HomePoints;
                    else if (string.Equals(apiGame.AwayTeam, localGame.ApiAwayTeam, StringComparison.OrdinalIgnoreCase))
                        newScore = apiGame.AwayPoints;

                    if (newScore != localGame.TeamAwayScore)
                    {
                        localGame.TeamAwayScore = newScore;
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
