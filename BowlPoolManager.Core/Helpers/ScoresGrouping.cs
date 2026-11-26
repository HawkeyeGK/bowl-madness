using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>The Scores page groups: Today, Coming up, Earlier results.</summary>
    public sealed record ScoreGroups(IReadOnlyList<BowlGame> Today, IReadOnlyList<BowlGame> ComingUp, IReadOnlyList<BowlGame> Earlier);

    public static class ScoresGrouping
    {
        /// <summary>
        /// "Today" is the Central calendar day (times are shown in Central, BUSINESS-RULES section 7).
        /// Today: games starting today, games being played, and games from earlier days that were never marked Final.
        /// Coming up: games on later days, earliest first. Earlier: games that are Final and did not start today, latest first.
        /// </summary>
        public static ScoreGroups Group(IEnumerable<BowlGame> games, DateTime nowUtc)
        {
            var today = DateTimeHelper.ToCentral(nowUtc).Date;
            var todayGames = new List<BowlGame>();
            var comingUp = new List<BowlGame>();
            var earlier = new List<BowlGame>();

            foreach (var game in games)
            {
                var startDay = DateTimeHelper.ToCentral(game.StartTime).Date;
                if (game.Status == GameStatus.InProgress) todayGames.Add(game);
                else if (game.Status == GameStatus.Final)
                {
                    if (startDay == today) todayGames.Add(game); else earlier.Add(game);
                }
                else if (startDay > today) comingUp.Add(game);
                else todayGames.Add(game); // today, or an earlier game still waiting for its result
            }

            return new ScoreGroups(
                todayGames.OrderBy(g => g.StartTime).ToList(),
                comingUp.OrderBy(g => g.StartTime).ToList(),
                earlier.OrderByDescending(g => g.StartTime).ToList());
        }
    }
}
