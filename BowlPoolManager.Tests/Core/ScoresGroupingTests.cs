using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    public class ScoresGroupingTests
    {
        // 2026-12-27 20:00 UTC is 2:00 PM Central (CST, UTC-6) on Dec 27.
        private static readonly DateTime Now = new(2026, 12, 27, 20, 0, 0, DateTimeKind.Utc);

        private static BowlGame Game(string id, DateTime startUtc, GameStatus status = GameStatus.Scheduled) =>
            new() { Id = id, BowlName = id, StartTime = startUtc, Status = status };

        private static DateTime Utc(int month, int day, int hour) => new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Group_ShouldPutAGameTonightInCentralTimeInToday_EvenWhenItsUtcDateIsTomorrow()
        {
            // 10:00 PM Central on Dec 27 is already Dec 28 in UTC.
            var result = ScoresGrouping.Group(new[] { Game("late", Utc(12, 28, 4)) }, Now);

            result.Today.Select(g => g.Id).Should().Equal("late");
            result.ComingUp.Should().BeEmpty();
        }

        [Fact]
        public void Group_ShouldPutAGameAfterCentralMidnightInComingUp()
        {
            // 1:00 AM Central on Dec 28.
            var result = ScoresGrouping.Group(new[] { Game("next", Utc(12, 28, 7)) }, Now);

            result.ComingUp.Select(g => g.Id).Should().Equal("next");
            result.Today.Should().BeEmpty();
        }

        [Fact]
        public void Group_ShouldPutAFinalGameFromEarlierDaysInEarlier()
        {
            var result = ScoresGrouping.Group(new[] { Game("old", Utc(12, 26, 18), GameStatus.Final) }, Now);

            result.Earlier.Select(g => g.Id).Should().Equal("old");
        }

        [Fact]
        public void Group_ShouldKeepAFinalGameFromTodayInToday()
        {
            var result = ScoresGrouping.Group(new[] { Game("done", Utc(12, 27, 15), GameStatus.Final) }, Now);

            result.Today.Select(g => g.Id).Should().Equal("done");
            result.Earlier.Should().BeEmpty();
        }

        [Fact]
        public void Group_ShouldKeepALiveGameInToday_WhenItStartedBeforeMidnight()
        {
            // Started yesterday evening, still being played after midnight Central.
            var now = Utc(12, 28, 7);
            var result = ScoresGrouping.Group(new[] { Game("live", Utc(12, 28, 3), GameStatus.InProgress) }, now);

            result.Today.Select(g => g.Id).Should().Equal("live");
        }

        [Fact]
        public void Group_ShouldKeepAnUnfinishedGameFromEarlierDaysInToday_WhenItWasNeverMarkedFinal()
        {
            var result = ScoresGrouping.Group(new[] { Game("stale", Utc(12, 26, 18)) }, Now);

            result.Today.Select(g => g.Id).Should().Equal("stale");
            result.Earlier.Should().BeEmpty();
        }

        [Fact]
        public void Group_ShouldOrderTodayAndComingUpEarliestFirst_AndEarlierLatestFirst()
        {
            var games = new[]
            {
                Game("t2", Utc(12, 27, 23)), Game("t1", Utc(12, 27, 17)),
                Game("c2", Utc(12, 31, 18)), Game("c1", Utc(12, 29, 18)),
                Game("e1", Utc(12, 20, 18), GameStatus.Final), Game("e2", Utc(12, 24, 18), GameStatus.Final)
            };

            var result = ScoresGrouping.Group(games, Now);

            result.Today.Select(g => g.Id).Should().Equal("t1", "t2");
            result.ComingUp.Select(g => g.Id).Should().Equal("c1", "c2");
            result.Earlier.Select(g => g.Id).Should().Equal("e2", "e1");
        }

        [Fact]
        public void Group_ShouldReturnThreeEmptyGroups_WhenThereAreNoGames()
        {
            var result = ScoresGrouping.Group(Array.Empty<BowlGame>(), Now);

            result.Today.Should().BeEmpty();
            result.ComingUp.Should().BeEmpty();
            result.Earlier.Should().BeEmpty();
        }

        [Fact]
        public void Group_ShouldPutAFinalGameDatedInTheFutureInEarlier_WhenItIsAlreadyDone()
        {
            // A mock feed can finish a game early; it is over, so it is a result.
            var result = ScoresGrouping.Group(new[] { Game("early", Utc(12, 30, 18), GameStatus.Final) }, Now);

            result.Earlier.Select(g => g.Id).Should().Equal("early");
        }
    }
}
