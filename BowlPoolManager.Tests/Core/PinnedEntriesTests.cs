using FluentAssertions;
using BowlPoolManager.Core.Dtos;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    public class PinnedEntriesTests
    {
        private static LeaderboardDto Row(string id, string name, int rank) => new() { Id = id, PlayerName = name, Rank = rank };

        private static readonly List<LeaderboardDto> Board = new()
        {
            Row("a", "Grandma Jo", 1), Row("b", "Uncle Mike", 2), Row("c", "Sally", 3), Row("d", "Ellie", 4)
        };

        [Fact]
        public void Find_ShouldPinNothing_WhenNobodyIsRememberedOrSignedIn()
        {
            var result = PinnedEntries.Find(Board, null, Array.Empty<string>());

            result.Rows.Should().BeEmpty();
            result.RememberedMissing.Should().BeFalse();
        }

        [Fact]
        public void Find_ShouldPinTheRememberedRow_WhenItIsOnTheBoard()
        {
            var result = PinnedEntries.Find(Board, new RememberedEntry("c", "Sally"), Array.Empty<string>());

            result.Rows.Select(r => r.Id).Should().Equal("c");
        }

        [Fact]
        public void Find_ShouldReportRememberedMissing_WhenTheEntryIsNotOnTheBoard()
        {
            var result = PinnedEntries.Find(Board, new RememberedEntry("gone", "Old Name"), Array.Empty<string>());

            result.Rows.Should().BeEmpty();
            result.RememberedMissing.Should().BeTrue();
        }

        [Fact]
        public void Find_ShouldFollowTheEntryById_WhenItWasRenamed()
        {
            var result = PinnedEntries.Find(Board, new RememberedEntry("c", "Sally (old name)"), Array.Empty<string>());

            result.Rows.Single().PlayerName.Should().Be("Sally");
        }

        [Fact]
        public void Find_ShouldPinAllSignedInEntriesInRankOrder_WhenThePlayerHasSeveral()
        {
            var result = PinnedEntries.Find(Board, null, new[] { "d", "b" });

            result.Rows.Select(r => r.Id).Should().Equal("b", "d");
        }

        [Fact]
        public void Find_ShouldNotRepeatARow_WhenRememberedAndSignedInAreTheSameEntry()
        {
            var result = PinnedEntries.Find(Board, new RememberedEntry("b", "Uncle Mike"), new[] { "b" });

            result.Rows.Select(r => r.Id).Should().Equal("b");
        }

        [Fact]
        public void Find_ShouldPinBoth_WhenRememberedIsAnotherEntryThanTheSignedInOnes()
        {
            var result = PinnedEntries.Find(Board, new RememberedEntry("a", "Grandma Jo"), new[] { "d" });

            result.Rows.Select(r => r.Id).Should().Equal("a", "d");
        }

        [Fact]
        public void Find_ShouldIgnoreSignedInIdsFromOtherPools()
        {
            var result = PinnedEntries.Find(Board, null, new[] { "elsewhere" });

            result.Rows.Should().BeEmpty();
        }

        [Fact]
        public void Find_ShouldPinNothing_WhenTheBoardIsEmpty()
        {
            var result = PinnedEntries.Find(new List<LeaderboardDto>(), new RememberedEntry("c", "Sally"), new[] { "c" });

            result.Rows.Should().BeEmpty();
        }
    }
}
