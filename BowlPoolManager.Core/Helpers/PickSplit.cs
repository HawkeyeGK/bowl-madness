using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>How many entries picked each team of a game.</summary>
    public sealed record PickSplitResult(int Home, int Away)
    {
        public int Total => Home + Away;
        public double HomePercent => Total == 0 ? 0 : 100.0 * Home / Total;
        public double AwayPercent => Total == 0 ? 0 : 100.0 * Away / Total;
    }

    /// <summary>
    /// The pool's pick split for the Scores page. Shown only after lock (BUSINESS-RULES section 3): before then the
    /// server hides picks from everyone but Admins, and this refuses to count them either, so an Admin's page never shows it early.
    /// </summary>
    public static class PickSplit
    {
        /// <returns>The split, or null when the pool is not locked, a team is not decided yet, or no pick was counted.</returns>
        public static PickSplitResult? ForGame(BowlGame game, IEnumerable<BracketEntry> entries, bool poolLocked)
        {
            if (!poolLocked) return null;
            if (BracketSlots.IsPlaceholder(game.TeamHome) || BracketSlots.IsPlaceholder(game.TeamAway)) return null;

            int home = 0, away = 0;
            foreach (var entry in entries)
            {
                if (entry.Picks == null || !entry.Picks.TryGetValue(game.Id, out var pick) || string.IsNullOrWhiteSpace(pick)) continue;
                if (string.Equals(pick, game.TeamHome, StringComparison.OrdinalIgnoreCase)) home++;
                else if (string.Equals(pick, game.TeamAway, StringComparison.OrdinalIgnoreCase)) away++;
            }

            return home + away == 0 ? null : new PickSplitResult(home, away);
        }
    }
}
