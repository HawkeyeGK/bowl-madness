using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>The leaderboard rows to pin at the top, and whether the remembered entry has gone from the pool.</summary>
    public sealed record PinnedEntries(IReadOnlyList<LeaderboardDto> Rows, bool RememberedMissing)
    {
        /// <summary>
        /// The player's own rows: every entry of the signed-in player in this pool, plus the entry this browser
        /// remembers. Matching is by entry id, so a renamed entry is still found. Rows come back in rank order, each once.
        /// </summary>
        /// <param name="rows">The pool's leaderboard rows.</param>
        /// <param name="remembered">What the browser remembers for this pool, or null.</param>
        /// <param name="signedInEntryIds">Ids of the signed-in player's entries (any pool; ids from other pools match nothing).</param>
        public static PinnedEntries Find(IReadOnlyList<LeaderboardDto> rows, RememberedEntry? remembered, IReadOnlyCollection<string> signedInEntryIds)
        {
            var wanted = new HashSet<string>(signedInEntryIds, StringComparer.Ordinal);
            if (remembered != null) wanted.Add(remembered.EntryId);

            var pinned = rows
                .Where(r => wanted.Contains(r.Id))
                .OrderBy(r => r.Rank)
                .ThenBy(r => r.PlayerName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var missing = remembered != null && rows.All(r => r.Id != remembered.EntryId);
            return new PinnedEntries(pinned, missing);
        }
    }
}
