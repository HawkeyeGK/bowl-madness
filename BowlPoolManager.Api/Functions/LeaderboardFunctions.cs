using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;
using BowlPoolManager.Core.Helpers;
using BowlPoolManager.Api.Repositories;

namespace BowlPoolManager.Api.Functions
{
    public class LeaderboardFunctions
    {
        private readonly ILogger _logger;
        private readonly IGameRepository _gameRepo;
        private readonly IEntryRepository _entryRepo;
        private readonly IPoolRepository _poolRepo;

        public LeaderboardFunctions(
            ILoggerFactory loggerFactory,
            IGameRepository gameRepo,
            IEntryRepository entryRepo,
            IPoolRepository poolRepo)
        {
            _logger = loggerFactory.CreateLogger<LeaderboardFunctions>();
            _gameRepo = gameRepo;
            _entryRepo = entryRepo;
            _poolRepo = poolRepo;
        }

        [Function("GetLeaderboard")]
        public async Task<HttpResponseData> GetLeaderboard([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Getting leaderboard.");

            var poolId = req.Query["poolId"];
            var seasonId = req.Query["seasonId"];

            if (string.IsNullOrEmpty(poolId) || string.IsNullOrEmpty(seasonId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteStringAsync("Both poolId and seasonId are required.");
                return badReq;
            }

            // 1. Fetch Pool Config (for tiebreaker rules and GameIds filter)
            var pool = await _poolRepo.GetPoolAsync(poolId);
            if (pool == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Pool not found.");
                return notFound;
            }

            // 2. Fetch All Games for Season
            var allGames = await _gameRepo.GetGamesAsync(seasonId);

            // 3. Filter Games by Pool's GameIds (if configured)
            List<BowlGame> games;
            if (pool.GameIds != null && pool.GameIds.Any())
            {
                games = allGames.Where(g => pool.GameIds.Contains(g.Id)).ToList();
            }
            else
            {
                games = allGames.ToList();
            }

            // 4. Fetch All Entries for Pool
            var entries = await _entryRepo.GetEntriesAsync(seasonId, poolId);

            // 5. Calculate Leaderboard using ScoringEngine
            var leaderboardRows = ScoringEngine.Calculate(games, entries, pool);

            // 6. Calculate totalFinalGames for UI display
            int totalFinalGames = games.Count(g => g.Status == GameStatus.Final);

            // 7. Map LeaderboardRow -> LeaderboardDto
            var leaderboardDtos = leaderboardRows.Select(row => new LeaderboardDto
            {
                Id = row.Entry.Id,
                PlayerName = row.Entry.PlayerName,
                Rank = row.Rank,
                Score = row.Score,
                MaxPossible = row.MaxPossible,
                CorrectPicks = row.CorrectPicks,
                TieBreakerPoints = row.Entry.TieBreakerPoints,
                TieBreakerDelta = row.TieBreakerDelta,
                IsPaid = row.Entry.IsPaid,
                RoundScores = row.RoundScores
            }).ToList();

            // 8. Return Response with metadata
            var responseData = new
            {
                totalFinalGames,
                leaderboard = leaderboardDtos
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(responseData);
            return response;
        }
    }
}
