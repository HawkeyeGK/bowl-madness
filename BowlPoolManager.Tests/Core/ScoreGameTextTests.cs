using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    public class ScoreGameTextTests
    {
        // 2026-12-27 20:00 UTC is 2:00 PM Central on Dec 27.
        private static readonly DateTime Now = new(2026, 12, 27, 20, 0, 0, DateTimeKind.Utc);

        private static BowlGame Game(DateTime startUtc, GameStatus status = GameStatus.Scheduled, string? detail = null) =>
            new() { Id = "g", StartTime = startUtc, Status = status, GameDetail = detail };

        [Fact]
        public void StatusText_ShouldShowTheTimeOnly_WhenTheGameIsLaterToday()
        {
            // 7:30 PM Central Dec 27 = 01:30 UTC Dec 28.
            ScoreGameText.StatusText(Game(new DateTime(2026, 12, 28, 1, 30, 0, DateTimeKind.Utc)), Now).Should().Be("7:30 PM CT");
        }

        [Fact]
        public void StatusText_ShouldShowDayAndTime_WhenTheGameIsOnALaterDay()
        {
            // 2:30 PM Central Dec 31 = 20:30 UTC.
            ScoreGameText.StatusText(Game(new DateTime(2026, 12, 31, 20, 30, 0, DateTimeKind.Utc)), Now).Should().Be("Thu Dec 31 · 2:30 PM CT");
        }

        [Fact]
        public void StatusText_ShouldShowTheFeedDetail_WhenTheGameIsLive()
        {
            ScoreGameText.StatusText(Game(Now.AddHours(-1), GameStatus.InProgress, "3rd • 4:12"), Now).Should().Be("3rd • 4:12");
        }

        [Fact]
        public void StatusText_ShouldSayInProgress_WhenALiveGameHasNoDetail()
        {
            ScoreGameText.StatusText(Game(Now.AddHours(-1), GameStatus.InProgress), Now).Should().Be("In progress");
        }

        [Fact]
        public void StatusText_ShouldSayFinalOnly_WhenTheGameWasToday()
        {
            ScoreGameText.StatusText(Game(Now.AddHours(-3), GameStatus.Final), Now).Should().Be("Final");
        }

        [Fact]
        public void StatusText_ShouldAddTheDay_WhenTheGameEndedOnAnEarlierDay()
        {
            ScoreGameText.StatusText(Game(new DateTime(2026, 12, 26, 18, 0, 0, DateTimeKind.Utc), GameStatus.Final), Now).Should().Be("Final · Sat Dec 26");
        }

        [Theory]
        [InlineData(TournamentRound.Standard, 1, "1 pt")]
        [InlineData(TournamentRound.Standard, 2, "2 pts")]
        [InlineData(TournamentRound.Round1, 3, "3 pts · Playoff 1st round")]
        [InlineData(TournamentRound.QuarterFinal, 4, "4 pts · Quarterfinal")]
        [InlineData(TournamentRound.SemiFinal, 5, "5 pts · Semifinal")]
        [InlineData(TournamentRound.Championship, 6, "6 pts · Championship")]
        public void PointsText_ShouldNameThePointsAndTheRound(TournamentRound round, int points, string expected)
        {
            var game = new BowlGame { PointValue = points, Round = round, IsPlayoff = round != TournamentRound.Standard };

            ScoreGameText.PointsText(game).Should().Be(expected);
        }
    }
}
