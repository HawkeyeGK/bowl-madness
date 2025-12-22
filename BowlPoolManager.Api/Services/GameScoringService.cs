using Microsoft.Extensions.Logging;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Api.Services
{
    public class GameScoringService : IGameScoringService
    {
        private readonly ILogger<GameScoringService> _logger;
        private readonly ICosmosDbService _cosmosService;
        private readonly ICfbdService _cfbdService;

        // Throttling State
        private static DateTime _lastRefresh = DateTime.MinValue;
        private static readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);
        private const int RefreshIntervalMinutes = 2;

        public GameScoringService(ILogger<GameScoringService> logger, ICosmosDbService cosmosService, ICfbdService cfbdService)
        {
            _logger = logger;
            _cosmosService = cosmosService;
            _cfbdService = cfbdService;
        }

        public async Task CheckAndRefreshScoresAsync(List<BowlGame> games)
        {
            // Quick check before waiting on lock
            if (DateTime.UtcNow <= _lastRefresh.AddMinutes(RefreshIntervalMinutes))
            {
                return;
            }

            await _refreshLock.WaitAsync();
            try
            {
                // Double-check inside lock
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

                // 1. MATCH HOME SCORE (Swap-Aware)
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

                // 2. MATCH AWAY SCORE (Swap-Aware)
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

                // CFBD Status: "scheduled", "in_progress", "final"
                if (apiGame.Completed || apiGame.StatusRaw == "final")
                {
                    localGame.Status = GameStatus.Final;
                    localGame.GameDetail = "Final";
                }
                else if (apiGame.StatusRaw == "in_progress" || (DateTime.UtcNow >= localGame.StartTime.AddMinutes(-15) && !apiGame.Completed))
                {
                    localGame.Status = GameStatus.InProgress;

                    // Format Time: "3rd • 10:45"
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
                    await _cosmosService.UpdateGameAsync(localGame);
                    anyChanged = true;
                }
            }

            if (anyChanged) _logger.LogInformation("Scores and status updated successfully.");
        }
    }
}
