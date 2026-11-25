using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>
    /// The top three places pay out. An entry is out when its best possible score is below the score of the
    /// entry in 3rd place (BUSINESS-RULES section 4). With three or fewer entries nobody can be out.
    /// Used by <see cref="BracketSimulator"/> and by the leaderboard's "out" marker, so both agree.
    /// </summary>
    public static class TopThreeRule
    {
        /// <param name="scoresInRankOrder">Every entry's score, best rank first.</param>
        /// <returns>The 3rd place score, or null when there are three or fewer entries.</returns>
        public static int? ThirdPlaceScore(IReadOnlyList<int> scoresInRankOrder) =>
            scoresInRankOrder.Count > 3 ? scoresInRankOrder[2] : null;

        public static bool CanFinishTop3(int maxPossible, int? thirdPlaceScore) =>
            thirdPlaceScore == null || maxPossible >= thirdPlaceScore;

        /// <summary>The ids of the leaderboard rows that can no longer finish in the top 3. Rows may arrive in any order.</summary>
        public static HashSet<string> OutEntryIds(IReadOnlyList<LeaderboardDto> rows)
        {
            var ordered = rows.OrderBy(r => r.Rank).ToList();
            var third = ThirdPlaceScore(ordered.Select(r => r.Score).ToList());
            return ordered.Where(r => !CanFinishTop3(r.MaxPossible, third)).Select(r => r.Id).ToHashSet();
        }
    }
}
