using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>Text on the printed entry form that depends on the pool and season (issues 125 and 126).</summary>
    public static class PrintFormText
    {
        /// <summary>
        /// The tiebreaker line, naming the pool's own tiebreaker game, or null when the pool has none (or its game
        /// is not among <paramref name="games"/>) so the form can leave the line out instead of naming a stale game.
        /// </summary>
        public static string? TieBreakerLine(BowlPool? pool, IEnumerable<BowlGame> games)
        {
            if (pool == null || string.IsNullOrEmpty(pool.TieBreakerGameId)) return null;

            var game = games.FirstOrDefault(g => string.Equals(g.Id, pool.TieBreakerGameId, StringComparison.OrdinalIgnoreCase));
            if (game == null) return null;

            return $"Tiebreaker: Total points in the {game.BowlName} ({game.TeamHome} vs {game.TeamAway})";
        }

        /// <summary>
        /// "BOWL MADNESS 2026": the year comes from the season (its SeasonId text), then the pool's own year.
        /// Never from the clock. With no usable year the title has none.
        /// </summary>
        public static string Title(string name, BowlPool? pool, Season? season)
        {
            if (season != null && int.TryParse(season.SeasonId, out var seasonYear) && seasonYear > 0)
                return $"{name} {seasonYear}";
            if (pool != null && pool.Season > 0)
                return $"{name} {pool.Season}";
            return name;
        }

        /// <summary>The points a game of <paramref name="round"/> is worth in this pool, or null when the pool has none.</summary>
        public static int? PointsFor(IEnumerable<BowlGame> games, TournamentRound round) =>
            games.FirstOrDefault(g => round == TournamentRound.Standard ? !g.IsPlayoff : g.IsPlayoff && g.Round == round)?.PointValue;

        private static readonly (TournamentRound Round, string Label)[] RoundLabels =
        {
            (TournamentRound.Standard, "Bowls"), (TournamentRound.Round1, "First round"), (TournamentRound.QuarterFinal, "Quarters"),
            (TournamentRound.SemiFinal, "Semis"), (TournamentRound.Championship, "Final"),
        };

        /// <summary>"Bowls 1 pt · First round 3 · Quarters 4 · Semis 5 · Final 10", leaving out rounds the pool does not have.</summary>
        public static string PointsLine(IEnumerable<BowlGame> games)
        {
            var list = games.ToList();
            var parts = new List<string>();
            foreach (var (round, label) in RoundLabels)
            {
                if (PointsFor(list, round) is not int points) continue;
                parts.Add(parts.Count == 0 ? $"{label} {points} pt{(points == 1 ? "" : "s")}" : $"{label} {points}");
            }
            return string.Join(" · ", parts);
        }
    }
}
