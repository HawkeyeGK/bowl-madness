using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Client.Helpers
{
    public static class BracketLayoutHelper
    {
        /// <summary>
        /// Returns the display order (0 = top of bracket) for standard R64 seed matchups.
        /// Unknown or null matchups return 99 and sort to the bottom.
        /// </summary>
        public static int GetSeedMatchupOrder(string? matchUp) => matchUp switch
        {
            "1v16" => 0,
            "8v9"  => 1,
            "5v12" => 2,
            "4v13" => 3,
            "6v11" => 4,
            "3v14" => 5,
            "7v10" => 6,
            "2v15" => 7,
            _      => 99
        };

        /// <summary>
        /// Returns the visual order key for any game by recursively resolving to its
        /// earliest R64 ancestor's seed matchup order. Used to sort feeder games so that
        /// the team visually on top in the previous round appears in the top slot of the
        /// next round's matchup.
        /// </summary>
        public static int GetVisualOrderKey(HoopsGame game, List<HoopsGame> allGames)
        {
            if (game.Round == TournamentRound.RoundOf64)
                return GetSeedMatchupOrder(game.SeedMatchup);

            var feeders = allGames.Where(g => g.NextGameId == game.Id).ToList();
            return feeders.Count > 0
                ? feeders.Min(f => GetVisualOrderKey(f, allGames))
                : 99;
        }

        /// <summary>
        /// Orders <paramref name="currentRound"/> games so each follows the position of its
        /// first feeder in <paramref name="previousRound"/> (ordered by feeder index, ascending).
        /// Games with no matching feeder sort last (index 99).
        /// </summary>
        public static List<HoopsGame> OrderByFeeder(
            IEnumerable<HoopsGame> currentRound,
            List<HoopsGame> previousRound)
        {
            return currentRound
                .OrderBy(g =>
                {
                    int idx = previousRound.FindIndex(prev => prev.NextGameId == g.Id);
                    return idx >= 0 ? idx : 99;
                })
                .ToList();
        }

        /// <summary>
        /// Recursively removes downstream picks that depended on <paramref name="oldTeam"/>
        /// winning <paramref name="fromGameId"/>. Modifies <paramref name="picks"/> in-place.
        /// </summary>
        public static void CascadeClear(
            string fromGameId,
            string oldTeam,
            Dictionary<string, string> picks,
            List<HoopsGame> allGames)
        {
            var fromGame = allGames.FirstOrDefault(g => g.Id == fromGameId);
            if (fromGame?.NextGameId == null) return;

            var nextGame = allGames.FirstOrDefault(g => g.Id == fromGame.NextGameId);
            if (nextGame == null) return;

            if (picks.TryGetValue(nextGame.Id, out var nextPick) &&
                string.Equals(nextPick, oldTeam, StringComparison.OrdinalIgnoreCase))
            {
                picks.Remove(nextGame.Id);
                CascadeClear(nextGame.Id, oldTeam, picks, allGames);
            }
        }
    }
}
