namespace BowlPoolManager.Core.Helpers
{
    /// <summary>
    /// Finds the pool an invite code opens (issue 135). Codes are reused from year to year (BUSINESS-RULES
    /// section 2), so when a code matches more than one pool the current season's pool wins.
    /// </summary>
    public static class InviteCodeLookup
    {
        /// <param name="pools">Candidate pools (any season).</param>
        /// <param name="code">What the player typed.</param>
        /// <param name="currentSeasonIds">Every id the current season goes by (the season's id, and its year text for hoops pools, see BUSINESS-RULES section 6).</param>
        /// <param name="fallbackToAnySeason">
        /// False (football): only a current-season pool can be joined by code. True (hoops): when no pool of the
        /// current season matches, take the first open match, because hoops pools do not always carry the season's id.
        /// </param>
        public static T? Find<T>(IEnumerable<T> pools, string? code, IEnumerable<string?> currentSeasonIds,
            Func<T, string?> inviteCode, Func<T, string?> seasonId, Func<T, bool> isArchived, bool fallbackToAnySeason = false) where T : class
        {
            var wanted = (code ?? string.Empty).Trim();
            if (wanted.Length == 0) return null;

            var currentIds = new HashSet<string>(currentSeasonIds.Where(s => !string.IsNullOrEmpty(s))!, StringComparer.OrdinalIgnoreCase);

            var matches = pools
                .Where(p => !isArchived(p) && string.Equals((inviteCode(p) ?? string.Empty).Trim(), wanted, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var current = matches.FirstOrDefault(p => !string.IsNullOrEmpty(seasonId(p)) && currentIds.Contains(seasonId(p)!));
            if (current != null) return current;

            return fallbackToAnySeason ? matches.FirstOrDefault() : null;
        }
    }
}
