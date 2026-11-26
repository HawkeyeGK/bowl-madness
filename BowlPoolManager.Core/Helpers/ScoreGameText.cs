using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>The words on a game card of the Scores page. Times are Central with "CT" (BUSINESS-RULES section 7).</summary>
    public static class ScoreGameText
    {
        /// <summary>Kickoff time (day first when not today), the live clock from the feed, or "Final" (with the day when it was not today).</summary>
        public static string StatusText(BowlGame game, DateTime nowUtc)
        {
            var isToday = DateTimeHelper.ToCentral(game.StartTime).Date == DateTimeHelper.ToCentral(nowUtc).Date;

            return game.Status switch
            {
                GameStatus.InProgress => string.IsNullOrWhiteSpace(game.GameDetail) ? "In progress" : game.GameDetail,
                GameStatus.Final => isToday ? "Final" : "Final · " + DateTimeHelper.ToCentral(game.StartTime).ToString("ddd MMM d"),
                _ => isToday
                    ? DateTimeHelper.ToCentralLabel(game.StartTime, "h:mm tt")
                    : DateTimeHelper.ToCentralLabel(game.StartTime, "ddd MMM d · h:mm tt")
            };
        }

        /// <summary>"1 pt", or "4 pts · Quarterfinal" for a playoff game.</summary>
        public static string PointsText(BowlGame game)
        {
            var points = $"{game.PointValue} {(game.PointValue == 1 ? "pt" : "pts")}";
            var round = game.Round switch
            {
                TournamentRound.Round1 => "Playoff 1st round",
                TournamentRound.QuarterFinal => "Quarterfinal",
                TournamentRound.SemiFinal => "Semifinal",
                TournamentRound.Championship => "Championship",
                _ => null
            };
            return round == null ? points : $"{points} · {round}";
        }
    }
}
