using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>A pool's games for the print pages: the games the pool lists, or every game of its season when it lists none.</summary>
    public static class PoolGames
    {
        public static List<BowlGame> Of(BowlPool pool, IEnumerable<BowlGame> seasonGames) =>
            (pool.GameIds != null && pool.GameIds.Any()
                ? seasonGames.Where(g => pool.GameIds.Contains(g.Id))
                : seasonGames)
            .Where(g => g.SeasonId == pool.SeasonId)
            .ToList();
    }
}
