using Microsoft.Extensions.Logging;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Api.Repositories;

namespace BowlPoolManager.Api.Services
{
    public class HoopsGameScoringService : IHoopsGameScoringService
    {
        private readonly ILogger<HoopsGameScoringService> _logger;
        private readonly IHoopsGameRepository _gameRepo;

        public HoopsGameScoringService(ILogger<HoopsGameScoringService> logger, IHoopsGameRepository gameRepo)
        {
            _logger = logger;
            _gameRepo = gameRepo;
        }

        public async Task ProcessGameUpdateAsync(HoopsGame game)
        {
            // 0. Fetch current DB state and all games for context
            var allGames = await _gameRepo.GetGamesAsync(game.SeasonId);

            // 1. Before saving, preserve propagated team data from completed feeders.
            //    This prevents the UI from overwriting good data with stale placeholders.
            var feeders = allGames
                .Where(g => string.Equals(g.NextGameId, game.Id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(g => g.Id) // stable sort — hoops games have no StartTime
                .ToList();

            foreach (var (feeder, index) in feeders.Select((f, i) => (f, i)))
            {
                if (feeder.Status == GameStatus.Final && !string.IsNullOrEmpty(feeder.WinningTeamName))
                {
                    var winnerName = feeder.WinningTeamName;
                    var winnerInfo = string.Equals(winnerName, feeder.TeamHome, StringComparison.OrdinalIgnoreCase)
                        ? feeder.HomeTeamInfo
                        : feeder.AwayTeamInfo;
                    var winnerSeed = string.Equals(winnerName, feeder.TeamHome, StringComparison.OrdinalIgnoreCase)
                        ? feeder.TeamHomeSeed
                        : feeder.TeamAwaySeed;

                    bool isHomeSlot = (feeders.Count > 1) ? (index == 0) : DetermineSlotForSingleFeeder(game, winnerName);

                    if (isHomeSlot && IsPlaceholder(game.TeamHome))
                    {
                        game.TeamHome = winnerName;
                        game.HomeTeamInfo = winnerInfo;
                        game.TeamHomeSeed = winnerSeed;
                        _logger.LogInformation($"ProcessGameUpdate: Preserved propagated Home team '{winnerName}' for game '{game.Id}'");
                    }
                    else if (!isHomeSlot && IsPlaceholder(game.TeamAway))
                    {
                        game.TeamAway = winnerName;
                        game.AwayTeamInfo = winnerInfo;
                        game.TeamAwaySeed = winnerSeed;
                        _logger.LogInformation($"ProcessGameUpdate: Preserved propagated Away team '{winnerName}' for game '{game.Id}'");
                    }
                }
            }

            // 2. Prepare batch updates
            var batchUpdates = new List<HoopsGame> { game };

            // 3. Propagate to downstream games if this game links forward
            if (!string.IsNullOrEmpty(game.NextGameId))
            {
                var index = allGames.FindIndex(g => g.Id == game.Id);
                if (index != -1) allGames[index] = game;

                PropagateWinner(game, allGames, batchUpdates);
            }

            // 4. Persist all changes in one batch
            await _gameRepo.SaveGamesAsBatchAsync(batchUpdates, game.SeasonId);
        }

        public async Task ForcePropagateAllAsync(string seasonId)
        {
            var allGames = await _gameRepo.GetGamesAsync(seasonId);

            // Process completed games in a stable order (feeders before targets)
            // For hoops, sort by round (earlier rounds first) then by Id for stability
            var completedGames = allGames
                .Where(g => g.Status == GameStatus.Final)
                .OrderBy(g => (int)g.Round)
                .ThenBy(g => g.Id)
                .ToList();

            _logger.LogInformation($"ForceHoopsPropagateAllAsync: Processing {completedGames.Count} completed games for season {seasonId}");

            var batchUpdates = new List<HoopsGame>();

            foreach (var game in completedGames)
            {
                if (!string.IsNullOrEmpty(game.NextGameId))
                {
                    PropagateWinner(game, allGames, batchUpdates);
                }
            }

            if (batchUpdates.Any())
            {
                _logger.LogInformation($"ForceHoopsPropagateAllAsync: Saving {batchUpdates.Count} propagated updates.");
                await _gameRepo.SaveGamesAsBatchAsync(batchUpdates, seasonId);
            }

            _logger.LogInformation($"ForceHoopsPropagateAllAsync: Completed propagation for season {seasonId}");
        }

        private void PropagateWinner(HoopsGame completedGame, List<HoopsGame> context, List<HoopsGame> pendingUpdates)
        {
            if (string.IsNullOrEmpty(completedGame.NextGameId)) return;

            var nextGame = pendingUpdates.FirstOrDefault(g => string.Equals(g.Id, completedGame.NextGameId, StringComparison.OrdinalIgnoreCase))
                           ?? context.FirstOrDefault(g => string.Equals(g.Id, completedGame.NextGameId, StringComparison.OrdinalIgnoreCase));

            if (nextGame == null)
            {
                _logger.LogWarning($"PropagateWinner: Could not find NextGame '{completedGame.NextGameId}' for game '{completedGame.Id}'");
                return;
            }

            // Identify feeders to determine home/away slot assignment
            var feeders = context
                .Where(g => string.Equals(g.NextGameId, nextGame.Id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(g => (int)g.Round)
                .ThenBy(g => g.Id)
                .ToList();

            bool isHomeSlot = true;

            var myIndex = feeders.FindIndex(f => string.Equals(f.Id, completedGame.Id, StringComparison.OrdinalIgnoreCase));

            if (myIndex >= 0)
            {
                if (feeders.Count > 1)
                {
                    isHomeSlot = (myIndex == 0);
                }
                else
                {
                    var winnerName = completedGame.WinningTeamName;

                    if (!string.IsNullOrEmpty(winnerName))
                    {
                        bool homeHasWinner = string.Equals(nextGame.TeamHome, winnerName, StringComparison.OrdinalIgnoreCase);
                        bool awayHasWinner = string.Equals(nextGame.TeamAway, winnerName, StringComparison.OrdinalIgnoreCase);

                        if (homeHasWinner && !awayHasWinner)
                        {
                            isHomeSlot = true;
                        }
                        else if (awayHasWinner && !homeHasWinner)
                        {
                            isHomeSlot = false;
                        }
                        else
                        {
                            bool homeIsPlaceholder = IsPlaceholder(nextGame.TeamHome);
                            bool awayIsPlaceholder = IsPlaceholder(nextGame.TeamAway);
                            if (!homeIsPlaceholder && awayIsPlaceholder) isHomeSlot = false;
                        }
                    }
                    else
                    {
                        bool homeIsPlaceholder = IsPlaceholder(nextGame.TeamHome);
                        bool awayIsPlaceholder = IsPlaceholder(nextGame.TeamAway);
                        if (!homeIsPlaceholder && awayIsPlaceholder) isHomeSlot = false;
                    }
                }
            }
            else
            {
                _logger.LogWarning($"PropagateWinner: Game '{completedGame.Id}' not found in feeders list for NextGame '{nextGame.Id}'.");
            }

            bool changed = false;

            if (completedGame.Status == GameStatus.Final && !string.IsNullOrEmpty(completedGame.WinningTeamName))
            {
                var winnerName = completedGame.WinningTeamName;
                var winnerInfo = string.Equals(winnerName, completedGame.TeamHome, StringComparison.OrdinalIgnoreCase)
                    ? completedGame.HomeTeamInfo
                    : completedGame.AwayTeamInfo;
                var winnerSeed = string.Equals(winnerName, completedGame.TeamHome, StringComparison.OrdinalIgnoreCase)
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
                string placeholder = GetRevertPlaceholder(completedGame);

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
                if (!pendingUpdates.Contains(nextGame))
                {
                    pendingUpdates.Add(nextGame);
                }

                _logger.LogInformation($"PropagateWinner: Updated game '{nextGame.Id}' — {nextGame.TeamHome} vs {nextGame.TeamAway}");

                PropagateWinner(nextGame, context, pendingUpdates);
            }
        }

        private bool DetermineSlotForSingleFeeder(HoopsGame targetGame, string winnerName)
        {
            if (string.Equals(targetGame.TeamHome, winnerName, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(targetGame.TeamAway, winnerName, StringComparison.OrdinalIgnoreCase)) return false;

            bool homeIsPlaceholder = IsPlaceholder(targetGame.TeamHome);
            bool awayIsPlaceholder = IsPlaceholder(targetGame.TeamAway);

            if (!homeIsPlaceholder && awayIsPlaceholder) return false;
            return true;
        }

        private static string GetRevertPlaceholder(HoopsGame game)
        {
            if (!string.IsNullOrEmpty(game.SeedMatchup) && !string.IsNullOrEmpty(game.Region))
                return $"Winner of {game.Region} {game.SeedMatchup}";
            if (!string.IsNullOrEmpty(game.Region))
                return $"Winner of {game.Region} {game.Round}";
            return "TBD";
        }

        private static bool IsPlaceholder(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            if (name.Equals("TBD", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.StartsWith("Winner of", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
