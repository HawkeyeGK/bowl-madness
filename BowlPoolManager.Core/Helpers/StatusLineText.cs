namespace BowlPoolManager.Core.Helpers
{
    /// <summary>The one line of status text under a page title. Times are Central with "CT" (BUSINESS-RULES section 7).</summary>
    public static class StatusLineText
    {
        private const string Separator = " · ";

        /// <summary>
        /// Before lock: when picks lock. After lock: when scores were last updated and how many games are final.
        /// The lock date itself still accepts changes (BUSINESS-RULES section 3), so lock text shows through it.
        /// </summary>
        /// <param name="lastUpdateUtc">When scores last refreshed; null or <see cref="DateTime.MinValue"/> when unknown.</param>
        /// <param name="totalGames">Games in the pool; 0 when unknown.</param>
        public static string ForPool(DateTime nowUtc, DateTime lockDateUtc, DateTime? lastUpdateUtc, int finalGames, int totalGames)
        {
            if (nowUtc <= lockDateUtc)
                return "Picks lock " + DateTimeHelper.ToCentralLabel(lockDateUtc, "ddd MMM d, h:mm tt");

            var parts = new List<string>();
            if (lastUpdateUtc.HasValue && lastUpdateUtc.Value != DateTime.MinValue)
                parts.Add("Updated " + DateTimeHelper.ToCentralLabel(lastUpdateUtc.Value, "h:mm tt"));

            if (finalGames > 0)
            {
                parts.Add(totalGames > 0
                    ? $"{finalGames} of {totalGames} games final"
                    : $"{finalGames} {(finalGames == 1 ? "game" : "games")} final");
            }

            return string.Join(Separator, parts);
        }
    }
}
