using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Api.Helpers;

namespace BowlPoolManager.Api.Functions
{
    public class ArchiveFunctions
    {
        private readonly ILogger _logger;
        private readonly IPoolRepository _poolRepo;
        private readonly IGameRepository _gameRepo;
        private readonly IEntryRepository _entryRepo;
        private readonly IArchiveRepository _archiveRepo;
        private readonly IUserRepository _userRepo;

        public ArchiveFunctions(ILoggerFactory loggerFactory,
            IPoolRepository poolRepo,
            IGameRepository gameRepo,
            IEntryRepository entryRepo,
            IArchiveRepository archiveRepo,
            IUserRepository userRepo)
        {
            _logger = loggerFactory.CreateLogger<ArchiveFunctions>();
            _poolRepo = poolRepo;
            _gameRepo = gameRepo;
            _entryRepo = entryRepo;
            _archiveRepo = archiveRepo;
            _userRepo = userRepo;
        }

        [Function("ArchivePool")]
        public async Task<HttpResponseData> ArchivePool(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ArchivePool/{poolId}")] HttpRequestData req,
            string poolId)
        {
            _logger.LogInformation($"Archiving pool {poolId}.");
            
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

            // 3. Fetch Data
            // Games (Filter by SeasonId of the pool)
            var allGames = await _gameRepo.GetGamesAsync();
            var seasonGames = allGames.Where(g => g.SeasonId == pool.SeasonId).ToList();

            // Entries
            var entries = await _entryRepo.GetEntriesAsync(poolId);

            // 4. Create Snapshot
            var archive = new PoolArchive
            {
                Id = $"Archive_{pool.Id}",
                PoolId = pool.Id,
                PoolName = pool.Name,
                SeasonId = pool.SeasonId,
                ArchivedOn = DateTime.UtcNow
            };

            // Map Games
            foreach (var g in seasonGames)
            {
                archive.Games.Add(new ArchiveGame
                {
                    GameId = g.Id,
                    BowlName = g.BowlName,
                    TeamHome = g.TeamHome,
                    TeamHomeScore = g.TeamHomeScore,
                    TeamAway = g.TeamAway,
                    TeamAwayScore = g.TeamAwayScore,
                    PointValue = g.PointValue
                });
            }

            // Calculate & Map Standings
            var calculatedStandings = new List<ArchiveStanding>();

            foreach (var entry in entries)
            {
                var standing = new ArchiveStanding
                {
                    PlayerName = entry.PlayerName,
                    TieBreakerPoints = entry.TieBreakerPoints
                };
                
                // Calculate Points
                int points = 0;
                foreach (var pick in entry.Picks) // Dictionary<GameId, string>
                {
                    var game = seasonGames.FirstOrDefault(g => g.Id == pick.Key);
                    if (game == null) continue;

                    // Add to Archives picks map
                    standing.Picks[pick.Key] = pick.Value;

                    // Calculate score
                    if (game.TeamHomeScore.HasValue && game.TeamAwayScore.HasValue)
                    {
                        string winner = "";
                        if (game.TeamHomeScore > game.TeamAwayScore) winner = game.TeamHome;
                        else if (game.TeamAwayScore > game.TeamHomeScore) winner = game.TeamAway;
                        
                        if (pick.Value == winner)
                        {
                            points += game.PointValue;
                        }
                    }
                }
                standing.TotalPoints = points;
                calculatedStandings.Add(standing);
            }

            // Sort and Rank
            calculatedStandings = calculatedStandings
                .OrderByDescending(s => s.TotalPoints)
                .ThenByDescending(s => s.TieBreakerPoints)
                .ToList();

            // Standard Competition Ranking (1224)
            for (int i = 0; i < calculatedStandings.Count; i++)
            {
                if (i > 0 && calculatedStandings[i].TotalPoints == calculatedStandings[i-1].TotalPoints)
                {
                    calculatedStandings[i].Rank = calculatedStandings[i-1].Rank;
                }
                else
                {
                    calculatedStandings[i].Rank = i + 1;
                }
            }
            
            archive.Standings = calculatedStandings;

            // 5. Transaction
            await _archiveRepo.AddArchiveAsync(archive);
            
            pool.IsArchived = true;
            await _poolRepo.AddPoolAsync(pool);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(archive);
            return response;
        }

        [Function("GetArchive")]
        public async Task<HttpResponseData> GetArchive(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Archives/{poolId}")] HttpRequestData req,
            string poolId)
        {
            _logger.LogInformation($"Getting archive for pool {poolId}.");
            
            // Construct the deterministic ID
            var archiveId = $"Archive_{poolId}";
            
            // We can try fetching directly using the Repo if it supported ID-based lookup without PartitionKey
            // But our Repo uses SeasonId as partition key.
            // However, GetArchiveAsync in the Repo implementation I wrote earlier does:
            // "SELECT * FROM c WHERE c.id = @id"
            // This is a cross-partition query if PK is not provided, which is acceptable for single-item lookups on low volume.
            
            var archive = await _archiveRepo.GetArchiveAsync(archiveId);
            
            if (archive == null) return req.CreateResponse(HttpStatusCode.NotFound);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(archive);
            return response;
        }
    }
}
