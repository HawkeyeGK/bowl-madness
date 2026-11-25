using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    public enum PickOutcome
    {
        /// <summary>The entry has no pick for this game.</summary>
        NoPick,
        /// <summary>The game is not decided yet.</summary>
        Pending,
        Correct,
        Wrong
    }

    /// <summary>Whether an entry's pick for a game was right. A game is decided only when it is Final with both scores and a winner (BUSINESS-RULES section 4).</summary>
    public static class PickOutcomes
    {
        public static PickOutcome For(BowlGame game, string? pick)
        {
            if (string.IsNullOrWhiteSpace(pick)) return PickOutcome.NoPick;

            if (game.Status != GameStatus.Final || game.TeamHomeScore is not int home || game.TeamAwayScore is not int away || home == away)
                return PickOutcome.Pending;

            var winner = home > away ? game.TeamHome : game.TeamAway;
            return string.Equals(pick, winner, StringComparison.OrdinalIgnoreCase) ? PickOutcome.Correct : PickOutcome.Wrong;
        }
    }
}
