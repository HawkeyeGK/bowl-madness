using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>One line of the Rules page's points list: "Quarterfinals", "4 points each".</summary>
    public record RoundPointsLine(string Label, string Points);

    /// <summary>
    /// The pool's own values on the Rules page (issue 34): lock time, points per round and tiebreakers, in plain words.
    /// Football only; hoops gets its own rules in Phase 3.
    /// </summary>
    public static class RulesText
    {
        private static readonly (TournamentRound Round, string Label)[] Rounds =
        {
            (TournamentRound.Standard, "Bowl games"), (TournamentRound.Round1, "First round"),
            (TournamentRound.QuarterFinal, "Quarterfinals"), (TournamentRound.SemiFinal, "Semifinals"),
            (TournamentRound.Championship, "Championship"),
        };

        /// <summary>
        /// Points per round, in round order, leaving out rounds the pool has no games in. Points are set per game, so a
        /// round whose games differ shows a range ("1 to 3 points each").
        /// </summary>
        public static IReadOnlyList<RoundPointsLine> PointsByRound(IEnumerable<BowlGame> poolGames)
        {
            var games = poolGames.ToList();
            var lines = new List<RoundPointsLine>();
            foreach (var (round, label) in Rounds)
            {
                // By Round alone, as ScoreGameText does: a playoff game whose round was never set counts with the bowls
                // instead of dropping out of the list.
                var inRound = games.Where(g => g.Round == round).ToList();
                if (inRound.Count == 0) continue;

                var low = inRound.Min(g => g.PointValue);
                var high = inRound.Max(g => g.PointValue);
                var amount = low == high ? $"{low} point{(low == 1 ? "" : "s")}" : $"{low} to {high} points";
                lines.Add(new RoundPointsLine(label, inRound.Count > 1 ? $"{amount} each" : amount));
            }
            return lines;
        }

        /// <summary>The pool's tiebreakers in the order they are used; listed once when both are the same.</summary>
        public static IReadOnlyList<string> TieBreakers(BowlPool pool, IEnumerable<BowlGame> games)
        {
            var game = games.FirstOrDefault(g => string.Equals(g.Id, pool.TieBreakerGameId, StringComparison.OrdinalIgnoreCase));
            var gameName = string.IsNullOrEmpty(game?.BowlName) ? "the tiebreaker game" : $"the {game.BowlName}";

            string Describe(TieBreakerMetric metric) => metric == TieBreakerMetric.CorrectPickCount
                ? "The number of correct picks"
                : $"How close your tiebreaker guess is to the total points scored in {gameName}";

            return pool.PrimaryTieBreaker == pool.SecondaryTieBreaker
                ? new[] { Describe(pool.PrimaryTieBreaker) }
                : new[] { Describe(pool.PrimaryTieBreaker), Describe(pool.SecondaryTieBreaker) };
        }

        /// <summary>
        /// "Picks lock Sun, Dec 20 at 11:00 AM CT." Changes are allowed up to and including the lock time
        /// (BUSINESS-RULES section 3), so the pool is locked only after it.
        /// </summary>
        public static string LockLine(DateTime lockUtc, DateTime nowUtc)
        {
            var when = DateTimeHelper.ToCentralLabel(lockUtc, "ddd, MMM d 'at' h:mm tt");
            return nowUtc > lockUtc ? $"Picks locked {when}." : $"Picks lock {when}.";
        }
    }
}
