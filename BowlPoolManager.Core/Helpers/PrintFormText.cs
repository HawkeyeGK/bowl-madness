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
    }
}
