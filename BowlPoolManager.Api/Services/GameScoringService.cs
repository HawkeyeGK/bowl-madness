using Microsoft.Extensions.Logging;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;
using BowlPoolManager.Api.Repositories; // NEW

namespace BowlPoolManager.Api.Services
{
    public class GameScoringService : IGameScoringService
    {
        private readonly ILogger<GameScoringService> _logger;
        private readonly IGameRepository _gameRepo; // Changed from ICosmosDbService
        private readonly ICfbdService _cfbdService;

        // Throttling State
        private static DateTime _lastRefresh = DateTime.MinValue;
        private static readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);
        private const int RefreshIntervalMinutes = 2;

        // Update Constructor Injection
        public GameScoringService(ILogger<GameScoringService> logger, IGameRepository gameRepo, ICfbdService cfbdService)
        {
            _logger = logger;
            _gameRepo = gameRepo;
            _cfbdService = cfbdService;
        }

        public DateTime GetLastRefreshTime()
        {
            return _lastRefresh;
        }

        public async Task CheckAndRefreshScoresAsync(List<BowlGame> games)
        {
            if (DateTime.UtcNow <= _lastRefresh.AddMinutes(RefreshIntervalMinutes)) return;

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

        private async Task PerformScoreUpdate(List<BowlGame> games)
        {
            var linkedGames = games
                .Where(g => !string.IsNullOrEmpty(g.ExternalId))
                .Where(g => g.Status != GameStatus.Final)
                .ToList();

            if (!linkedGames.Any()) return;

            var apiGames = await _cfbdService.GetScoreboardGamesAsync();
            bool anyChanged = false;

            foreach (var localGame in linkedGames)
            {
                var apiGame = apiGames.FirstOrDefault(x => x.Id.ToString() == localGame.ExternalId);
                if (apiGame == null) continue;

                bool gameChanged = false;

                // 1. MATCH HOME SCORE
                int? homeScore = null;
                if (!string.IsNullOrEmpty(localGame.ApiHomeTeam))
                {
                    if (string.Equals(apiGame.HomeTeam, localGame.ApiHomeTeam, StringComparison.OrdinalIgnoreCase))
                        homeScore = apiGame.HomePoints;
                    else if (string.Equals(apiGame.AwayTeam, localGame.ApiHomeTeam, StringComparison.OrdinalIgnoreCase))
                        homeScore = apiGame.AwayPoints;
                }

                if (homeScore.HasValue && homeScore != localGame.TeamHomeScore)
                {
                    localGame.TeamHomeScore = homeScore;
                    gameChanged = true;
                }

                // 2. MATCH AWAY SCORE
                int? awayScore = null;
                if (!string.IsNullOrEmpty(localGame.ApiAwayTeam))
                {
                    if (string.Equals(apiGame.HomeTeam, localGame.ApiAwayTeam, StringComparison.OrdinalIgnoreCase))
                        awayScore = apiGame.HomePoints;
                    else if (string.Equals(apiGame.AwayTeam, localGame.ApiAwayTeam, StringComparison.OrdinalIgnoreCase))
                        awayScore = apiGame.AwayPoints;
                }

                if (awayScore.HasValue && awayScore != localGame.TeamAwayScore)
                {
                    localGame.TeamAwayScore = awayScore;
                    gameChanged = true;
                }

                // 3. UPDATE STATUS & GAME DETAIL
                var oldStatus = localGame.Status;
                var oldDetail = localGame.GameDetail;

                if (apiGame.Completed || string.Equals(apiGame.StatusRaw, "completed", StringComparison.OrdinalIgnoreCase))
                {
                    localGame.Status = GameStatus.Final;
                    localGame.GameDetail = "Final";
                }
                else if (string.Equals(apiGame.StatusRaw, "in_progress", StringComparison.OrdinalIgnoreCase) || 
                        (DateTime.UtcNow >= localGame.StartTime.AddMinutes(-15) && !apiGame.Completed))
                {
                    localGame.Status = GameStatus.InProgress;

                    if (apiGame.Period.HasValue)
                    {
                        string p = apiGame.Period switch { 1 => "1st", 2 => "2nd", 3 => "3rd", 4 => "4th", _ => "OT" };
                        localGame.GameDetail = $"{p} • {apiGame.Clock ?? "00:00"}";
                    }
                    else
                    {
                        localGame.GameDetail = "In Progress";
                    }
                }

                if (localGame.Status != oldStatus || localGame.GameDetail != oldDetail)
                    gameChanged = true;

                if (gameChanged)
                {
                    // Call the Repository instead of the generic service
                    await _gameRepo.UpdateGameAsync(localGame);
                    
                    // Trigger Propagation immediately using the current list as context
                    await PropagateWinner(localGame, games);
                    
                    anyChanged = true;
                }
            }

            if (anyChanged) _logger.LogInformation("Scores and status updated successfully.");
        }

        public async Task ProcessGameUpdateAsync(BowlGame game)
        {
            // 1. Persist the primary update
            await _gameRepo.UpdateGameAsync(game);

            // 2. Check for propagation requirements
            if (!string.IsNullOrEmpty(game.NextGameId))
            {
                // We need the full context to determine slots
                var allGames = await _gameRepo.GetGamesAsync(game.SeasonId);
                await PropagateWinner(game, allGames);
            }
        }

        /// <summary>
        /// Forces propagation of all completed games in a season.
        /// This method fetches games once, then iterates through them in chronological order,
        /// ensuring that each propagation uses the same in-memory list to avoid stale data issues.
        /// </summary>
        public async Task ForcePropagateAllAsync(string seasonId)
        {
            var allGames = await _gameRepo.GetGamesAsync(seasonId);
            
            // Process completed games in chronological order
            // This ensures feeder games are processed before target games
            var completedGames = allGames
                .Where(g => g.Status == GameStatus.Final)
                .OrderBy(g => g.StartTime)
                .ToList();

            _logger.LogInformation($"ForcePropagateAllAsync: Processing {completedGames.Count} completed games for season {seasonId}");

            foreach (var game in completedGames)
            {
                if (!string.IsNullOrEmpty(game.NextGameId))
                {
                    // Use the SAME list for all calls - modifications are in-memory and carried forward
                    await PropagateWinner(game, allGames);
                }
            }
            
            _logger.LogInformation($"ForcePropagateAllAsync: Completed propagation for season {seasonId}");
        }

        private async Task PropagateWinner(BowlGame completedGame, List<BowlGame> context)
        {
            if (string.IsNullOrEmpty(completedGame.NextGameId)) return;

            var nextGame = context.FirstOrDefault(g => string.Equals(g.Id, completedGame.NextGameId, StringComparison.OrdinalIgnoreCase));
            if (nextGame == null)
            {
                _logger.LogWarning($"PropagateWinner: Could not find NextGame with Id '{completedGame.NextGameId}' for CompletedGame '{completedGame.BowlName}'");
                return;
            }

            // Identify feeders to determine slot
            var feeders = context
                .Where(g => string.Equals(g.NextGameId, nextGame.Id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(g => g.StartTime)
                .ToList();

            bool isHomeSlot = true; // Default to Home (earlier game logic)

            // Determine slot based on feed index
            var myIndex = feeders.FindIndex(f => string.Equals(f.Id, completedGame.Id, StringComparison.OrdinalIgnoreCase));
            
            if (myIndex >= 0)
            {
                // Found myself in the feeders list
                if (feeders.Count > 1)
                {
                    // If multiple feeders: 0 -> Home, 1 -> Away
                    isHomeSlot = (myIndex == 0);
                }
                else
                {
                    // Single feeder in list (myself)
                    // First, check if the winner already exists in one of the slots
                    // This prevents double-propagation on subsequent runs
                    var winnerName = completedGame.WinningTeamName;
                    
                    if (!string.IsNullOrEmpty(winnerName))
                    {
                        bool homeHasWinner = string.Equals(nextGame.TeamHome, winnerName, StringComparison.OrdinalIgnoreCase);
                        bool awayHasWinner = string.Equals(nextGame.TeamAway, winnerName, StringComparison.OrdinalIgnoreCase);
                        
                        if (homeHasWinner && !awayHasWinner)
                        {
                            // Winner already in Home, stay with Home
                            isHomeSlot = true;
                            _logger.LogInformation($"PropagateWinner: Single feeder '{completedGame.BowlName}'. Winner '{winnerName}' already in Home slot.");
                        }
                        else if (awayHasWinner && !homeHasWinner)
                        {
                            // Winner already in Away, stay with Away
                            isHomeSlot = false;
                            _logger.LogInformation($"PropagateWinner: Single feeder '{completedGame.BowlName}'. Winner '{winnerName}' already in Away slot.");
                        }
                        else
                        {
                            // Winner not found in either slot (or somehow in both), use placeholder heuristic
                            bool homeIsPlaceholder = IsPlaceholder(nextGame.TeamHome);
                            bool awayIsPlaceholder = IsPlaceholder(nextGame.TeamAway);

                            if (!homeIsPlaceholder && awayIsPlaceholder) 
                            {
                                isHomeSlot = false;
                            }
                            _logger.LogInformation($"PropagateWinner: Single feeder '{completedGame.BowlName}'. Slot guessed as: {(isHomeSlot ? "Home" : "Away")} (Home placeholder: {homeIsPlaceholder}, Away placeholder: {awayIsPlaceholder})");
                        }
                    }
                    else
                    {
                        // No winner yet, use placeholder heuristic
                        bool homeIsPlaceholder = IsPlaceholder(nextGame.TeamHome);
                        bool awayIsPlaceholder = IsPlaceholder(nextGame.TeamAway);

                        if (!homeIsPlaceholder && awayIsPlaceholder) isHomeSlot = false;
                        _logger.LogInformation($"PropagateWinner: Single feeder '{completedGame.BowlName}' (no winner). Slot guessed as: {(isHomeSlot ? "Home" : "Away")}");
                    }
                }
            }
            else
            {
                // Should not happen if data is consistent, but safeguard
                _logger.LogWarning($"PropagateWinner: CompletedGame '{completedGame.BowlName}' not found in feeders list for NextGame '{nextGame.BowlName}'.");
            }
            
            bool changed = false;

            if (completedGame.Status == GameStatus.Final && !string.IsNullOrEmpty(completedGame.WinningTeamName))
            {
                // PROPAGATE WINNER
                var winnerName = completedGame.WinningTeamName;
                var winnerInfo = (string.Equals(winnerName, completedGame.TeamHome, StringComparison.OrdinalIgnoreCase)) 
                    ? completedGame.HomeTeamInfo 
                    : completedGame.AwayTeamInfo;
                
                var winnerSeed = (string.Equals(winnerName, completedGame.TeamHome, StringComparison.OrdinalIgnoreCase))
                    ? completedGame.TeamHomeSeed
                    : completedGame.TeamAwaySeed;

                if (isHomeSlot)
                {
                    if (nextGame.TeamHome != winnerName)
                    {
                        nextGame.TeamHome = winnerName;
                        nextGame.HomeTeamInfo = winnerInfo;
                        nextGame.TeamHomeSeed = winnerSeed;
                        changed = true;
                    }
                }
                else
                {
                    if (nextGame.TeamAway != winnerName)
                    {
                        nextGame.TeamAway = winnerName;
                        nextGame.AwayTeamInfo = winnerInfo;
                        nextGame.TeamAwaySeed = winnerSeed;
                        changed = true;
                    }
                }
            }
            else
            {
                // REVERT / RESET (if game became non-final or we need to reset downstream)
                // Note: This is aggressive, but ensures consistency.
                string placeholder = $"Winner of {completedGame.BowlName}";
                
                if (isHomeSlot)
                {
                    if (nextGame.TeamHome != placeholder)
                    {
                        nextGame.TeamHome = placeholder;
                        nextGame.HomeTeamInfo = null;
                        nextGame.TeamHomeSeed = null;
                        changed = true;
                    }
                }
                else
                {
                    if (nextGame.TeamAway != placeholder)
                    {
                        nextGame.TeamAway = placeholder;
                        nextGame.AwayTeamInfo = null;
                        nextGame.TeamAwaySeed = null;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                await _gameRepo.UpdateGameAsync(nextGame);
                _logger.LogInformation($"PropagateWinner: Updated NextGame '{nextGame.BowlName}' {nextGame.TeamHome} vs {nextGame.TeamAway}");
                // Recursive propagation!
                await PropagateWinner(nextGame, context);
            }
        }

        private bool IsPlaceholder(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            if (name.StartsWith("Winner of", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.Equals("TBD", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
