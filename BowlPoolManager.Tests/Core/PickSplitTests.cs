using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    public class PickSplitTests
    {
        private static BowlGame Game(string home = "Iowa", string away = "Vanderbilt") =>
            new() { Id = "g1", BowlName = "Test Bowl", TeamHome = home, TeamAway = away };

        private static BracketEntry Entry(string id, string? pick, bool hidden = false) => new()
        {
            Id = id,
            Picks = hidden ? null : (pick == null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["g1"] = pick })
        };

        [Fact]
        public void ForGame_ShouldCountEachTeam_WhenThePoolIsLocked()
        {
            var entries = new[] { Entry("1", "Iowa"), Entry("2", "Iowa"), Entry("3", "Vanderbilt") };

            var split = PickSplit.ForGame(Game(), entries, poolLocked: true);

            split.Should().Be(new PickSplitResult(Home: 2, Away: 1));
        }

        [Fact]
        public void ForGame_ShouldGiveNothing_WhenThePoolIsNotLocked()
        {
            var entries = new[] { Entry("1", "Iowa"), Entry("2", "Vanderbilt") };

            PickSplit.ForGame(Game(), entries, poolLocked: false).Should().BeNull();
        }

        [Fact]
        public void ForGame_ShouldGiveNothing_WhenEveryEntryHidesItsPicks()
        {
            var entries = new[] { Entry("1", null, hidden: true), Entry("2", null, hidden: true) };

            PickSplit.ForGame(Game(), entries, poolLocked: true).Should().BeNull();
        }

        [Fact]
        public void ForGame_ShouldSkipEntriesWithoutAPickForTheGame()
        {
            var entries = new[] { Entry("1", "Iowa"), Entry("2", null), Entry("3", "") };

            PickSplit.ForGame(Game(), entries, poolLocked: true).Should().Be(new PickSplitResult(1, 0));
        }

        [Fact]
        public void ForGame_ShouldIgnoreAPickForAnotherTeam_WhenItMatchesNeitherSide()
        {
            var entries = new[] { Entry("1", "Iowa"), Entry("2", "Some Other Team") };

            PickSplit.ForGame(Game(), entries, poolLocked: true).Should().Be(new PickSplitResult(1, 0));
        }

        [Fact]
        public void ForGame_ShouldMatchTeamNamesIgnoringCase()
        {
            var entries = new[] { Entry("1", "iowa"), Entry("2", "VANDERBILT") };

            PickSplit.ForGame(Game(), entries, poolLocked: true).Should().Be(new PickSplitResult(1, 1));
        }

        [Theory]
        [InlineData("Winner of Cotton Bowl", "Ohio State")]
        [InlineData("Georgia", "TBD")]
        [InlineData("", "Georgia")]
        public void ForGame_ShouldGiveNothing_WhenOneTeamIsNotDecidedYet(string home, string away)
        {
            var entries = new[] { Entry("1", "Georgia"), Entry("2", "Ohio State") };

            PickSplit.ForGame(Game(home, away), entries, poolLocked: true).Should().BeNull();
        }

        [Fact]
        public void ForGame_ShouldReportTheShareOfEachSide()
        {
            var split = new PickSplitResult(Home: 7, Away: 5);

            split.Total.Should().Be(12);
            split.HomePercent.Should().BeApproximately(58.3, 0.1);
            split.AwayPercent.Should().BeApproximately(41.7, 0.1);
        }
    }
}
