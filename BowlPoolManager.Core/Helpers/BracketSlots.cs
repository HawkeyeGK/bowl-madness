using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>
    /// The one rule for which slot (home or away) of a playoff game a feeder game's winner fills.
    /// Start time sets the order: with two feeders the earlier kickoff goes to home. With one feeder
    /// the other slot already holds a team, so the winner fills the other slot. No field on the game
    /// records this; it is worked out from the games each time.
    /// </summary>
    public static class BracketSlots
    {
        public static bool IsPlaceholder(string? name) =>
            string.IsNullOrEmpty(name)
            || name.Equals("TBD", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Winner of", StringComparison.OrdinalIgnoreCase);

        public static string PlaceholderFor(BowlGame feeder) => $"Winner of {feeder.BowlName}";

        /// <summary>The games that feed <paramref name="next"/>, earliest kickoff first (id breaks ties).</summary>
        public static List<BowlGame> FeedersOf(BowlGame next, IEnumerable<BowlGame> all) =>
            all.Where(g => string.Equals(g.NextGameId, next.Id, StringComparison.OrdinalIgnoreCase))
               .OrderBy(g => g.StartTime)
               .ThenBy(g => g.Id, StringComparer.Ordinal)
               .ToList();

        /// <summary>True when the winner of <paramref name="feeder"/> fills the home slot of <paramref name="next"/>.</summary>
        public static bool IsHomeSlot(BowlGame feeder, BowlGame next, IEnumerable<BowlGame> all)
        {
            var feeders = FeedersOf(next, all);
            int index = feeders.FindIndex(f => string.Equals(f.Id, feeder.Id, StringComparison.OrdinalIgnoreCase));
            return IsHomeSlot(index, feeders.Count, next.TeamHome, next.TeamAway,
                feeder.TeamHome, feeder.TeamAway, PlaceholderFor(feeder));
        }

        /// <summary>
        /// Name-only form, for games that are not <see cref="BowlGame"/>. <paramref name="feederIndex"/> is the
        /// feeder's position among the feeders ordered by kickoff (-1 when it is not among them).
        /// <paramref name="feederOwnNames"/> are the names the feeder can leave in the slot: its two teams
        /// (the winner of an earlier result) and its "Winner of ..." placeholder.
        /// </summary>
        public static bool IsHomeSlot(int feederIndex, int feederCount, string? nextHome, string? nextAway, params string?[] feederOwnNames)
        {
            if (feederIndex < 0) return true;
            if (feederCount > 1) return feederIndex == 0;

            bool homeIsFeeders = IsFeedersSlot(nextHome, feederOwnNames);
            bool awayIsFeeders = IsFeedersSlot(nextAway, feederOwnNames);
            if (awayIsFeeders && !homeIsFeeders) return false;
            return true;
        }

        // A slot belongs to the feeder when it is empty, a placeholder, or still holds one of the feeder's teams.
        // Anything else is a team the next game already had (a seed).
        private static bool IsFeedersSlot(string? slot, string?[] feederOwnNames) =>
            IsPlaceholder(slot)
            || feederOwnNames.Any(n => !string.IsNullOrEmpty(n) && string.Equals(n, slot, StringComparison.OrdinalIgnoreCase));
    }
}
