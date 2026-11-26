namespace BowlPoolManager.Core.Helpers
{
    /// <summary>
    /// AP rank of a team in a game: typed by an admin on the games page (nullable, not read from the feed).
    /// The AP poll ranks 25 teams, so a typed rank is 1 to 25 or empty.
    /// </summary>
    public static class ApRank
    {
        public const int Max = 25;

        public static bool IsValid(int? rank) => rank == null || (rank >= 1 && rank <= Max);

        /// <summary>"#15 Utah" for a ranked team, "Utah" otherwise. For text; pages that draw the rank use <c>TeamPickButton</c> or <c>MatchupCard</c>.</summary>
        public static string Label(string teamName, int? rank) => rank is > 0 ? $"#{rank} {teamName}" : teamName;
    }
}
