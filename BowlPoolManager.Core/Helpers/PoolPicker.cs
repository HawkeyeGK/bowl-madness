namespace BowlPoolManager.Core.Helpers
{
    /// <summary>
    /// Which pool a page shows first. The same order the Leaderboard has always used: a pool named in the
    /// address, else the pool this browser last chose, else the pool of the player's newest entry, else the first pool.
    /// </summary>
    public static class PoolPicker
    {
        /// <param name="poolIds">The pools a visitor may see (not archived), in display order.</param>
        /// <param name="queryPoolId">A pool named in the address (?poolId=). When it is not in the list, the first pool is used.</param>
        /// <param name="savedPoolId">The pool this browser last chose.</param>
        /// <param name="latestEntryPoolId">The pool of the signed-in player's newest entry.</param>
        /// <returns>The chosen pool id, or null when there are no pools.</returns>
        public static string? Pick(IReadOnlyList<string> poolIds, string? queryPoolId, string? savedPoolId, string? latestEntryPoolId)
        {
            if (poolIds.Count == 0) return null;

            if (!string.IsNullOrEmpty(queryPoolId))
                return poolIds.Contains(queryPoolId) ? queryPoolId : poolIds[0];

            if (!string.IsNullOrEmpty(savedPoolId) && poolIds.Contains(savedPoolId)) return savedPoolId;
            if (!string.IsNullOrEmpty(latestEntryPoolId) && poolIds.Contains(latestEntryPoolId)) return latestEntryPoolId;
            return poolIds[0];
        }
    }
}
