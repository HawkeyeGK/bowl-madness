using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>
    /// Which season the player pages use, and where to look for an entry whose season the page does not know
    /// (owner decision 2026-11-28, issue 138).
    /// </summary>
    public static class CurrentSeason
    {
        /// <summary>
        /// The season marked current. When none is marked, the newest season by year (SeasonId "2025"), not by the
        /// GUID Id, which would pick an arbitrary one. Null when there are no seasons.
        /// </summary>
        public static Season? Pick(IEnumerable<Season> seasons)
        {
            var list = seasons.ToList();
            return list.FirstOrDefault(s => s.IsCurrent) ?? NewestFirst(list).FirstOrDefault();
        }

        /// <summary>
        /// Season ids (the partition keys) to try, in order, when looking up an entry by id alone: the current season
        /// first, then the others newest first. An old link to last year's entry is then found in its own season
        /// (business rules section 6: an entry is read with its own stored season).
        /// </summary>
        public static List<string> EntryLookupOrder(IEnumerable<Season> seasons)
        {
            var list = seasons.Where(s => !string.IsNullOrEmpty(s.Id)).ToList();
            var current = Pick(list);
            var order = new List<string>();
            if (current != null) order.Add(current.Id);
            order.AddRange(NewestFirst(list).Select(s => s.Id).Where(id => !order.Contains(id)));
            return order;
        }

        private static IEnumerable<Season> NewestFirst(IEnumerable<Season> seasons) =>
            seasons.OrderByDescending(s => int.TryParse(s.SeasonId, out var year) ? year : int.MinValue)
                   .ThenByDescending(s => s.SeasonId, StringComparer.Ordinal);
    }
}
