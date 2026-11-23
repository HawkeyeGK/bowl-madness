using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>
    /// Season values for a new football pool: the season link (Season.Id, a GUID) and the year (Season.SeasonId, "2025").
    /// </summary>
    public static class NewPoolSeason
    {
        /// <summary>Sets the season link and year on a pool that is being created.</summary>
        public static void Apply(BowlPool pool, IEnumerable<Season> seasons, int fallbackYear)
        {
            var (seasonId, year) = For(seasons, fallbackYear);
            pool.SeasonId = seasonId;
            pool.Season = year;
        }

        /// <summary>A new pool starts with every game of its season selected, unless games were already chosen.</summary>
        public static void SelectAllGames(BowlPool pool, IEnumerable<BowlGame> seasonGames)
        {
            if (pool.GameIds.Any()) return;
            pool.GameIds = seasonGames.Select(g => g.Id).ToList();
        }

        public static (string SeasonId, int Year) For(IEnumerable<Season> seasons, int fallbackYear)
        {
            var current = seasons.FirstOrDefault(s => s.IsCurrent);
            if (current != null)
            {
                var year = int.TryParse(current.SeasonId, out var parsed) ? parsed : fallbackYear;
                return (current.Id, year);
            }

            return (fallbackYear.ToString(), fallbackYear);
        }
    }
}
