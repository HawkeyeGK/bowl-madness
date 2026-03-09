using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Api.Helpers;

namespace BowlPoolManager.Api.Functions
{
    public class HoopsArchiveFunctions
    {
        private readonly ILogger _logger;
        private readonly IHoopsPoolRepository _poolRepo;
        private readonly IHoopsGameRepository _gameRepo;
        private readonly IHoopsEntryRepository _entryRepo;
        private readonly IArchiveRepository _archiveRepo;
        private readonly IUserRepository _userRepo;

        public HoopsArchiveFunctions(
            ILoggerFactory loggerFactory,
            IHoopsPoolRepository poolRepo,
            IHoopsGameRepository gameRepo,
            IHoopsEntryRepository entryRepo,
            IArchiveRepository archiveRepo,
            IUserRepository userRepo)
        {
            _logger = loggerFactory.CreateLogger<HoopsArchiveFunctions>();
            _poolRepo = poolRepo;
            _gameRepo = gameRepo;
            _entryRepo = entryRepo;
            _archiveRepo = archiveRepo;
            _userRepo = userRepo;
        }

        [Function("ArchiveHoopsPool")]
        public async Task<HttpResponseData> ArchiveHoopsPool(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ArchiveHoopsPool/{poolId}")] HttpRequestData req,
            string poolId)
        {
            _logger.LogInformation("Archiving hoops pool {PoolId}.", poolId);

            // 1. Auth Check (SuperAdmin)
            var authResult = await SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);
            if (!authResult.IsValid) return authResult.ErrorResponse!;

            // 2. Fetch Pool & Validate
            var pool = await _poolRepo.GetPoolAsync(poolId);
            if (pool == null) return req.CreateResponse(HttpStatusCode.NotFound);

            if (!pool.IsConcluded)
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteStringAsync("Pool must be concluded before archiving.");
                return badReq;
            }

            // 3. Fetch Games (use pool.SeasonId — may differ from season GUID; see known SeasonId quirk)
            var allGames = await _gameRepo.GetGamesAsync(pool.SeasonId);
            List<HoopsGame> games;
            if (pool.GameIds != null && pool.GameIds.Any())
                games = allGames.Where(g => pool.GameIds.Contains(g.Id)).ToList();
            else
                games = allGames.ToList();

            // 4. Hydrate PointValue from PointsPerRound (in-memory only)
            if (pool.PointsPerRound != null)
            {
                foreach (var game in games)
                {
                    if (pool.PointsPerRound.TryGetValue(game.Round, out var pts))
                        game.PointValue = pts;
                    else
                        _logger.LogWarning("HoopsGame {GameId} (Round={Round}) has no PointsPerRound entry in pool {PoolId}.", game.Id, game.Round, poolId);
                }
            }
            else
            {
                _logger.LogWarning("HoopsPool {PoolId} has no PointsPerRound configured.", poolId);
            }

            // 5. Fetch Entries (use pool.SeasonId — the stored partition key)
            var entries = await _entryRepo.GetEntriesAsync(pool.SeasonId, poolId);

            // 6. Create Snapshot
            var archive = new PoolArchive
            {
                Id = $"HoopsArchive_{pool.Id}",
                PoolId = pool.Id,
                PoolName = pool.Name,
                SeasonId = pool.SeasonId,
                Season = pool.Season,
                ArchivedOn = DateTime.UtcNow
            };

            // Map Games
            foreach (var g in games)
            {
                archive.Games.Add(new ArchiveGame
                {
                    GameId = g.Id,
                    StartTime = default, // HoopsGame has no StartTime; archive viewer sorts by Round
                    Round = g.Round,
                    Region = g.Region,
                    TeamHome = g.TeamHome,
                    TeamHomeSeed = g.TeamHomeSeed,
                    TeamHomeScore = g.TeamHomeScore,
                    TeamAway = g.TeamAway,
                    TeamAwaySeed = g.TeamAwaySeed,
                    TeamAwayScore = g.TeamAwayScore,
                    PointValue = g.PointValue
                });
            }

            // Calculate & Map Standings (no tiebreaker for basketball)
            var leaderboard = ScoringEngine.Calculate(games.Cast<IScorable>().ToList(), entries);

            archive.Standings = leaderboard.Select(row => new ArchiveStanding
            {
                PlayerName = row.Entry.PlayerName,
                Rank = row.Rank,
                TotalPoints = row.Score,
                CorrectPicks = row.CorrectPicks,
                TieBreakerPoints = row.Entry.TieBreakerPoints,
                TieBreakerDelta = row.TieBreakerDelta,
                Picks = row.Entry.Picks ?? new Dictionary<string, string>()
            }).ToList();

            // 7. Persist
            await _archiveRepo.AddArchiveAsync(archive);

            pool.IsArchived = true;
            await _poolRepo.AddPoolAsync(pool);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(archive);
            return response;
        }

        [Function("GetHoopsArchive")]
        public async Task<HttpResponseData> GetHoopsArchive(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "HoopsArchives/{poolId}")] HttpRequestData req,
            string poolId)
        {
            _logger.LogInformation("Getting hoops archive for pool {PoolId}.", poolId);

            var archiveId = $"HoopsArchive_{poolId}";
            var archive = await _archiveRepo.GetArchiveAsync(archiveId);

            if (archive == null) return req.CreateResponse(HttpStatusCode.NotFound);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(archive);
            return response;
        }
    }
}
