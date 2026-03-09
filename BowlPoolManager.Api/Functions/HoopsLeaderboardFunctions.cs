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
    public class HoopsLeaderboardFunctions
    {
        private readonly ILogger _logger;
        private readonly IHoopsGameRepository _gameRepo;
        private readonly IHoopsEntryRepository _entryRepo;
        private readonly IHoopsPoolRepository _poolRepo;

        public HoopsLeaderboardFunctions(
            ILoggerFactory loggerFactory,
            IHoopsGameRepository gameRepo,
            IHoopsEntryRepository entryRepo,
            IHoopsPoolRepository poolRepo)
        {
            _logger = loggerFactory.CreateLogger<HoopsLeaderboardFunctions>();
            _gameRepo = gameRepo;
            _entryRepo = entryRepo;
            _poolRepo = poolRepo;
        }

        [Function("GetHoopsLeaderboard")]
        public async Task<HttpResponseData> GetHoopsLeaderboard(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Getting hoops leaderboard.");

            var poolId = req.Query["poolId"];
            var seasonId = req.Query["seasonId"];

            if (string.IsNullOrEmpty(poolId) || string.IsNullOrEmpty(seasonId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteStringAsync("Both poolId and seasonId are required.");
                return badReq;
            }

            // 1. Fetch Pool Config
            var pool = await _poolRepo.GetPoolAsync(poolId);
            if (pool == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Pool not found.");
                return notFound;
            }

            // 2. Fetch All Games for Season (use pool.SeasonId — the stored value may differ from the query param)
            var allGames = await _gameRepo.GetGamesAsync(pool.SeasonId);

            // 3. Filter Games by Pool's GameIds (if configured)
            List<HoopsGame> games;
            if (pool.GameIds != null && pool.GameIds.Any())
            {
                games = allGames.Where(g => pool.GameIds.Contains(g.Id)).ToList();
            }
            else
            {
                games = allGames.ToList();
            }

            // 4. Hydrate PointValue from PointsPerRound (in-memory only — never persisted)
            if (pool.PointsPerRound != null)
            {
                foreach (var game in games)
                {
                    if (pool.PointsPerRound.TryGetValue(game.Round, out var pts))
                    {
                        game.PointValue = pts;
                    }
                    else
                    {
                        _logger.LogWarning(
                            "HoopsGame {GameId} (Round={Round}) has no PointsPerRound entry in pool {PoolId}.",
                            game.Id, game.Round, poolId);
                    }
                }
            }
            else
            {
                _logger.LogWarning("HoopsPool {PoolId} has no PointsPerRound configured.", poolId);
            }

            // Guard: warn if any game still has PointValue == 0 after hydration
            var unhydrated = games.Where(g => g.PointValue == 0).ToList();
            if (unhydrated.Any())
            {
                _logger.LogWarning(
                    "{Count} games in pool {PoolId} have PointValue=0 after hydration. Rounds: {Rounds}",
                    unhydrated.Count, poolId,
                    string.Join(", ", unhydrated.Select(g => g.Round).Distinct()));
            }

            // 5. Fetch All Entries for Pool (use pool.SeasonId — the stored value may differ from the query param)
            var entries = await _entryRepo.GetEntriesAsync(pool.SeasonId, poolId);

            // 6. Calculate Leaderboard using ScoringEngine (no tiebreaker for basketball)
            var leaderboardRows = ScoringEngine.Calculate(
                games.Cast<IScorable>().ToList(),
                entries);

            // 7. Calculate totalFinalGames for UI display
            int totalFinalGames = games.Count(g => g.Status == GameStatus.Final);

            // 8. Map LeaderboardRow -> LeaderboardDto
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
                SeasonId = row.Entry.SeasonId,
                RoundScores = row.RoundScores
            }).ToList();

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
