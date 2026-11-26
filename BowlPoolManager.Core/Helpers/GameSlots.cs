using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>A team slot of a game, as a page should show it.</summary>
    /// <param name="Name">The team, "Winner of Cotton Bowl", or "TBD".</param>
    /// <param name="Info">Logo and colors; null for a slot that is not decided.</param>
    /// <param name="Decided">False for "Winner of ..." and "TBD".</param>
    public sealed record ResolvedTeam(string Name, TeamInfo? Info, bool Decided);

    public static class GameSlots
    {
        /// <summary>
        /// The team in a slot. A slot that holds a team name shows it; an empty or "TBD" slot is worked out from the games that feed
        /// it (<see cref="BracketSlots"/>): the feeder's winner when it is decided, otherwise "Winner of {bowl}".
        /// </summary>
        public static ResolvedTeam Resolve(BowlGame game, bool isHome, IReadOnlyList<BowlGame> allGames)
        {
            var name = isHome ? game.TeamHome : game.TeamAway;
            var info = isHome ? game.HomeTeamInfo : game.AwayTeamInfo;

            if (!string.IsNullOrEmpty(name) && !name.Equals("TBD", StringComparison.OrdinalIgnoreCase))
                return new ResolvedTeam(name, info, !BracketSlots.IsPlaceholder(name));

            var feeders = BracketSlots.FeedersOf(game, allGames);
            var feeder = feeders.FirstOrDefault(f => BracketSlots.IsHomeSlot(f, game, allGames) == isHome);
            if (feeder == null) return new ResolvedTeam("TBD", null, false);

            if (feeder.IsFinal && !string.IsNullOrEmpty(feeder.WinningTeamName))
            {
                var winner = feeder.WinningTeamName;
                var winnerInfo = string.Equals(winner, feeder.TeamHome, StringComparison.OrdinalIgnoreCase) ? feeder.HomeTeamInfo : feeder.AwayTeamInfo;
                return new ResolvedTeam(winner, winnerInfo, true);
            }

            return new ResolvedTeam(BracketSlots.PlaceholderFor(feeder), null, false);
        }
    }
}
