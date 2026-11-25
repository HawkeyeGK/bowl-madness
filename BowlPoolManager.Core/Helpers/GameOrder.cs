using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>
    /// The one order for a football pool's games, shared by fast paper entry and the printed blank entry form. It is
    /// the same order My Picks and Entry Details use: regular bowls first, then playoff games, each group by kickoff
    /// (id breaks ties so the order never depends on the order the games arrive in).
    /// </summary>
    public static class GameOrder
    {
        public static List<BowlGame> ForEntry(IEnumerable<BowlGame> games) =>
            games.OrderBy(g => g.IsPlayoff)
                 .ThenBy(g => g.StartTime)
                 .ThenBy(g => g.Id, StringComparer.Ordinal)
                 .ToList();
    }
}
