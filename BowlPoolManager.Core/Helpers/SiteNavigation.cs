using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>One link in the site navigation. <see cref="Key"/> picks the icon in the client.</summary>
    /// <param name="Href">Relative to the site root, no leading slash (the page has base href "/"). Empty is the home page.</param>
    /// <param name="MatchExact">True when only the exact address is "current" (the home page).</param>
    public record NavItem(string Key, string Label, string Href, bool MatchExact = false);

    /// <summary>The links of the new layout, per site (Release 2 group A). Football and hoops share labels, not addresses.</summary>
    public static class SiteNavigation
    {
        public static string SiteName(Sport sport) => sport == Sport.Basketball ? "Hoops Madness" : "Bowl Madness";

        /// <summary>Label of the picks tab for a player who has no entry yet. Short enough for a phone tab.</summary>
        public const string EnterPicksLabel = "Enter Picks";
        public const string MyPicksLabel = "My Picks";

        /// <summary>
        /// The four bottom tabs on a phone and the main links on desktop.
        /// <paramref name="needsPicks"/> is true when the player has no entry yet (or is signed out): the picks tab then
        /// says "Enter Picks" so a new player knows where to go. Same address either way.
        /// </summary>
        public static IReadOnlyList<NavItem> Tabs(Sport sport, bool needsPicks = false)
        {
            var picksLabel = needsPicks ? EnterPicksLabel : MyPicksLabel;
            return sport == Sport.Basketball
                ? new[]
                {
                    new NavItem("leaderboard", "Leaderboard", "hoops/leaderboard"),
                    new NavItem("scores", "Scores", "hoops/scoreboard"),
                    new NavItem("whatif", "What If", "hoops/what-if"),
                    new NavItem("picks", picksLabel, "hoops/mypicks")
                }
                : new[]
                {
                    new NavItem("leaderboard", "Leaderboard", "", MatchExact: true),
                    new NavItem("scores", "Scores", "results"),
                    new NavItem("whatif", "What If", "what-if"),
                    new NavItem("picks", picksLabel, "mypicks")
                };
        }

        /// <summary>
        /// Links that are not tabs: the phone menu, and the desktop bar after the tabs.
        /// Rules is football only until hoops gets its own rules (Phase 3).
        /// </summary>
        public static IReadOnlyList<NavItem> MenuLinks(Sport sport) => sport == Sport.Basketball
            ? new[]
            {
                new NavItem("history", "History", "hoops/archives"),
                new NavItem("print", "Print Bracket", "hoops/print-bracket")
            }
            : new[]
            {
                new NavItem("history", "History", "archives"),
                new NavItem("print", "Print Bracket", "print-bracket"),
                new NavItem("rules", "Rules", "rules")
            };
    }
}
