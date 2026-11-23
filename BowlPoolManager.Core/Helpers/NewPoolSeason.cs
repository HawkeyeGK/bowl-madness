using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>
    /// Season values for a new football pool: the season link (Season.Id, a GUID) and the year (Season.SeasonId, "2025").
    /// </summary>
    public static class NewPoolSeason
    {
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
