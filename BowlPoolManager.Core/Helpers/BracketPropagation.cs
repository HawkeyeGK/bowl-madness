using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>
    /// Copies a game's winner into the next game's slot, or puts the "Winner of ..." placeholder back
    /// when the game is no longer final, then carries on down the bracket. The slot comes from
    /// <see cref="BracketSlots"/>, never from what the slot currently holds.
    /// </summary>
    public static class BracketPropagation
    {
        /// <param name="completedGame">The game whose result changed.</param>
        /// <param name="context">Every game in the season.</param>
        /// <param name="pendingUpdates">Games changed so far; the latest copy of a game is looked up here first.</param>
        /// <param name="warn">Called when the next game cannot be found.</param>
        public static void Propagate(BowlGame completedGame, List<BowlGame> context, List<BowlGame> pendingUpdates, Action<string>? warn = null)
        {
            if (string.IsNullOrEmpty(completedGame.NextGameId)) return;

            var nextGame = pendingUpdates.FirstOrDefault(g => string.Equals(g.Id, completedGame.NextGameId, StringComparison.OrdinalIgnoreCase))
                           ?? context.FirstOrDefault(g => string.Equals(g.Id, completedGame.NextGameId, StringComparison.OrdinalIgnoreCase));

            if (nextGame == null)
            {
                warn?.Invoke($"Could not find NextGame with Id '{completedGame.NextGameId}' for CompletedGame '{completedGame.BowlName}'");
                return;
            }

            bool isHomeSlot = BracketSlots.IsHomeSlot(completedGame, nextGame, context);
            bool changed = false;

            if (completedGame.Status == GameStatus.Final && !string.IsNullOrEmpty(completedGame.WinningTeamName))
            {
                var winnerName = completedGame.WinningTeamName;
                bool homeWon = string.Equals(winnerName, completedGame.TeamHome, StringComparison.OrdinalIgnoreCase);
                var winnerInfo = homeWon ? completedGame.HomeTeamInfo : completedGame.AwayTeamInfo;
                var winnerSeed = homeWon ? completedGame.TeamHomeSeed : completedGame.TeamAwaySeed;

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
                else if (nextGame.TeamAway != winnerName)
                {
                    nextGame.TeamAway = winnerName;
                    nextGame.AwayTeamInfo = winnerInfo;
                    nextGame.TeamAwaySeed = winnerSeed;
                    changed = true;
                }
            }
            else
            {
                string placeholder = BracketSlots.PlaceholderFor(completedGame);

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
                else if (nextGame.TeamAway != placeholder)
                {
                    nextGame.TeamAway = placeholder;
                    nextGame.AwayTeamInfo = null;
                    nextGame.TeamAwaySeed = null;
                    changed = true;
                }
            }

            if (changed)
            {
                if (!pendingUpdates.Contains(nextGame)) pendingUpdates.Add(nextGame);
                Propagate(nextGame, context, pendingUpdates, warn);
            }
        }
    }
}
