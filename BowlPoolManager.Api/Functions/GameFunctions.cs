using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Api.Services.ScoreFeed;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;
using BowlPoolManager.Core.Helpers;
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
        
        private readonly IScoreFeed _scoreFeed;
        private readonly ISeasonRepository _seasonRepo;
        private readonly IGameScoringService _scoringService;

        public GameFunctions(ILoggerFactory loggerFactory, 
                             IGameRepository gameRepo, // Injected
                             IUserRepository userRepo, // Injected
                             IScoreFeed scoreFeed,
                             ISeasonRepository seasonRepo,
                             IGameScoringService scoringService)
        {
            _logger = loggerFactory.CreateLogger<GameFunctions>();
            _gameRepo = gameRepo;
            _userRepo = userRepo;
            _scoreFeed = scoreFeed;
            _seasonRepo = seasonRepo;
            _scoringService = scoringService;
        }

        [Function("GetGames")]
        public async Task<HttpResponseData> GetGames([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            // Use Repo
            var seasonId = req.Query["seasonId"];
            var games = await _gameRepo.GetGamesAsync(seasonId);

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

            var year = await GetCurrentSeasonYearAsync();
            if (year == null) return await NoCurrentSeasonAsync(req);

            var games = (await _scoreFeed.GetPostseasonGamesAsync(year.Value))
                .Select(CfbdFeedMapping.ToCfbdDto).ToList();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(games);
            return response;
        }

        // The feed year is the current season's SeasonId ("2025"), never a literal (issue 102).
        private async Task<int?> GetCurrentSeasonYearAsync()
        {
            var seasons = await _seasonRepo.GetSeasonsAsync();
            var current = seasons.FirstOrDefault(s => s.IsCurrent);
            return current != null && int.TryParse(current.SeasonId, out var year) ? year : null;
        }

        private static async Task<HttpResponseData> NoCurrentSeasonAsync(HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteStringAsync("No current season is set.");
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
                var games = (await _scoreFeed.GetScoreboardAsync())
                    .Select(CfbdFeedMapping.ToCfbdDto).ToList();
                json = JsonSerializer.Serialize(games);
            }
            else
            {
                var year = await GetCurrentSeasonYearAsync();
                if (year == null) return await NoCurrentSeasonAsync(req);

                // CFBD: the provider's own JSON. Mock: the mock's games in the same shape.
                json = _scoreFeed is IRawPostseasonJsonSource raw
                    ? await raw.GetRawPostseasonJsonAsync(year.Value)
                    : JsonSerializer.Serialize((await _scoreFeed.GetPostseasonGamesAsync(year.Value))
                        .Select(CfbdFeedMapping.ToCfbdDto).ToList());
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

             try
             {
                 // Input handling matches SaveHoopsGame: an unreadable or empty body, or a game without an id or
                 // season (the partition key), is refused before anything is saved (issue 138).
                 BowlGame? game;
                 try
                 {
                     game = await JsonSerializer.DeserializeAsync<BowlGame>(req.Body);
                 }
                 catch (JsonException)
                 {
                     return await BadRequestAsync(req, "The game could not be read.");
                 }

                 if (game == null || string.IsNullOrEmpty(game.Id) || string.IsNullOrEmpty(game.SeasonId))
                     return await BadRequestAsync(req, "A game needs an id and a season.");

                 var resultProblem = ResultProblem(game.Status, game.TeamHomeScore, game.TeamAwayScore);
                 if (resultProblem != null) return await BadRequestAsync(req, resultProblem);

                 // AP rank is typed by hand: 1 to 25 or empty.
                 if (!ApRank.IsValid(game.HomeApRank) || !ApRank.IsValid(game.AwayApRank))
                     return await BadRequestAsync(req, $"An AP rank must be a number from 1 to {ApRank.Max}, or empty.");

                 await _scoringService.ProcessGameUpdateAsync(game);
                 return req.CreateResponse(HttpStatusCode.OK);
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "SaveGame failed.");
                 return req.CreateResponse(HttpStatusCode.InternalServerError);
             }
        }

        /// <summary>
        /// The Game Results page's save: only the status and the two scores. They are applied to the stored game, so
        /// fields the score feed or another admin changed since the page loaded are not overwritten (issue 138).
        /// </summary>
        [Function("SaveGameResult")]
        public async Task<HttpResponseData> SaveGameResult([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            try
            {
                GameResultUpdate? update;
                try
                {
                    update = await JsonSerializer.DeserializeAsync<GameResultUpdate>(req.Body);
                }
                catch (JsonException)
                {
                    return await BadRequestAsync(req, "The result could not be read.");
                }

                if (update == null || string.IsNullOrEmpty(update.GameId) || string.IsNullOrEmpty(update.SeasonId))
                    return await BadRequestAsync(req, "A result needs a game id and a season.");

                if (!Enum.IsDefined(update.Status))
                    return await BadRequestAsync(req, "The game status is not valid.");

                var resultProblem = ResultProblem(update.Status, update.TeamHomeScore, update.TeamAwayScore);
                if (resultProblem != null) return await BadRequestAsync(req, resultProblem);

                var games = await _gameRepo.GetGamesAsync(update.SeasonId);
                var game = games.FirstOrDefault(g => string.Equals(g.Id, update.GameId, StringComparison.OrdinalIgnoreCase));
                if (game == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("The game was not found in that season.");
                    return notFound;
                }

                game.Status = update.Status;
                game.TeamHomeScore = update.TeamHomeScore;
                game.TeamAwayScore = update.TeamAwayScore;

                await _scoringService.ProcessGameUpdateAsync(game);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(game);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveGameResult failed.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        // A game cannot be Final without two scores and a winner: games cannot end tied (issue 133).
        private static string? ResultProblem(GameStatus status, int? homeScore, int? awayScore) =>
            status == GameStatus.Final && (homeScore == null || awayScore == null || homeScore == awayScore)
                ? "A Final game needs both scores and a winner. Games cannot end tied."
                : null;

        private static async Task<HttpResponseData> BadRequestAsync(HttpRequestData req, string message)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync(message);
            return badRequest;
        }

        [Function("DeleteGame")]
        public async Task<HttpResponseData> DeleteGame([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "DeleteGame/{gameId}")] HttpRequestData req, string gameId)
        {
             var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
             if (!authResult.IsValid) return authResult.ErrorResponse!;

             var seasonId = req.Query["seasonId"];
             if (string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(seasonId))
             {
                 var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                 await badRequest.WriteStringAsync("Game ID and Season ID are required");
                 return badRequest;
             }

             await _gameRepo.DeleteGameAsync(gameId, seasonId);
             return req.CreateResponse(HttpStatusCode.OK);
        }

        [Function("ForcePropagation")]
        public async Task<HttpResponseData> ForcePropagation([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            var seasonId = req.Query["seasonId"];
            if (string.IsNullOrEmpty(seasonId))
            {
                 var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                 await badRequest.WriteStringAsync("Season ID is required");
                 return badRequest;
            }

            // Call the dedicated bulk propagation method
            await _scoringService.ForcePropagateAllAsync(seasonId);

            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
