using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>One team line of a playoff game on the printed form.</summary>
    /// <param name="Team">A team set in advance, or null for a blank line.</param>
    /// <param name="Seed">The set team's playoff seed, when known.</param>
    /// <param name="FeederBowl">For a blank line, the bowl whose winner fills it.</param>
    /// <param name="Written">For a blank line on a printed entry, the entry's pick for the feeder game.</param>
    /// <param name="Checked">On a printed entry, true when the entry picked this line to win the game.</param>
    public sealed record FormSlot(string? Team, int? Seed, string? FeederBowl, string? Written, bool Checked)
    {
        /// <summary>The name to print on the line: the set team, else the written pick, else nothing.</summary>
        public string? Shown => Team ?? Written;
    }

    /// <summary>
    /// Which teams a playoff game shows on the printed form (upgrade plan 5.4): both teams of a first-round game,
    /// the top seed of a quarterfinal, nothing in a semifinal or the championship. A slot a feeder game's winner will
    /// fill stays blank, even after the server copied a real winner into it, and names that feeder. With an entry's
    /// picks, each blank carries the pick for its feeder game and the entry's winner of each game is checked.
    /// </summary>
    public static class BlankFormSlots
    {
        public static (FormSlot Top, FormSlot Bottom) SlotsFor(BowlGame game, IEnumerable<BowlGame> allGames,
                                                              IReadOnlyDictionary<string, string>? picks = null)
        {
            var all = allGames.ToList();
            var feeders = BracketSlots.FeedersOf(game, all);
            var winnerPick = PickFor(game, picks);
            return (Slot(game, true, all, feeders, picks, winnerPick), Slot(game, false, all, feeders, picks, winnerPick));
        }

        /// <summary>The entry's pick for a game, or null when there is none.</summary>
        public static string? PickFor(BowlGame game, IReadOnlyDictionary<string, string>? picks) =>
            picks != null && picks.TryGetValue(game.Id, out var pick) && !string.IsNullOrWhiteSpace(pick) ? pick : null;

        /// <summary>True when the entry picked <paramref name="team"/> to win <paramref name="game"/>.</summary>
        public static bool IsPicked(BowlGame game, string? team, IReadOnlyDictionary<string, string>? picks) =>
            !string.IsNullOrEmpty(team) && string.Equals(PickFor(game, picks), team, StringComparison.OrdinalIgnoreCase);

        private static FormSlot Slot(BowlGame game, bool isHome, List<BowlGame> all, List<BowlGame> feeders,
                                     IReadOnlyDictionary<string, string>? picks, string? winnerPick)
        {
            var name = isHome ? game.TeamHome : game.TeamAway;
            bool isFeedersTeam = feeders.Any(f =>
                string.Equals(f.TeamHome, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(f.TeamAway, name, StringComparison.OrdinalIgnoreCase));

            if (!BracketSlots.IsPlaceholder(name) && !isFeedersTeam)
            {
                var seed = isHome ? game.TeamHomeSeed : game.TeamAwaySeed;
                return new FormSlot(name, seed, null, null, Matches(winnerPick, name));
            }

            var feeder = feeders.FirstOrDefault(f => BracketSlots.IsHomeSlot(f, game, all) == isHome);
            var written = feeder == null ? null : PickFor(feeder, picks);
            return new FormSlot(null, null, feeder?.BowlName, written, Matches(winnerPick, written));
        }

        private static bool Matches(string? pick, string? name) =>
            !string.IsNullOrEmpty(pick) && string.Equals(pick, name, StringComparison.OrdinalIgnoreCase);
    }
}
