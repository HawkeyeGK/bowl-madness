using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>
    /// Which teams a playoff game shows on the blank entry form (upgrade plan 5.4): both teams of a first-round game,
    /// the top seed of a quarterfinal, nothing in a semifinal or the championship. A slot a feeder game's winner will
    /// fill stays blank, even after the server copied a real winner into it.
    /// </summary>
    public static class BlankFormSlots
    {
        public static (string? Top, string? Bottom) SlotsFor(BowlGame game, IEnumerable<BowlGame> allGames)
        {
            var all = allGames.ToList();
            var feeders = BracketSlots.FeedersOf(game, all);
            return (Pick(game.TeamHome, feeders), Pick(game.TeamAway, feeders));
        }

        private static string? Pick(string? name, List<BowlGame> feeders)
        {
            if (BracketSlots.IsPlaceholder(name)) return null;
            bool isFeedersTeam = feeders.Any(f =>
                string.Equals(f.TeamHome, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(f.TeamAway, name, StringComparison.OrdinalIgnoreCase));
            return isFeedersTeam ? null : name;
        }
    }
}
