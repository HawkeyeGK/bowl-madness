using BowlPoolManager.Core.Dtos;
using BowlPoolManager.Core.Helpers;
using FluentAssertions;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>BUSINESS-RULES section 4: an entry is out when it can no longer finish in the top 3.</summary>
    public class TopThreeRuleTests
    {
        private static LeaderboardDto Row(string id, int rank, int score, int max) =>
            new() { Id = id, PlayerName = id, Rank = rank, Score = score, MaxPossible = max };

        [Fact]
        public void ThirdPlaceScore_ShouldBeNull_WhenThreeOrFewerEntries()
        {
            TopThreeRule.ThirdPlaceScore(new[] { 9, 5, 1 }).Should().BeNull();
            TopThreeRule.ThirdPlaceScore(Array.Empty<int>()).Should().BeNull();
        }

        [Fact]
        public void ThirdPlaceScore_ShouldBeTheThirdScoreInRankOrder_WhenMoreThanThreeEntries()
        {
            TopThreeRule.ThirdPlaceScore(new[] { 14, 13, 13, 12, 11 }).Should().Be(13);
        }

        [Fact]
        public void CanFinishTop3_ShouldBeTrue_WhenThereIsNoThirdPlaceScore()
        {
            TopThreeRule.CanFinishTop3(0, null).Should().BeTrue();
        }

        [Theory]
        [InlineData(13, 13, true)]
        [InlineData(14, 13, true)]
        [InlineData(12, 13, false)]
        public void CanFinishTop3_ShouldCompareMaxToThirdPlaceScore(int max, int third, bool expected)
        {
            TopThreeRule.CanFinishTop3(max, third).Should().Be(expected);
        }

        [Fact]
        public void OutEntryIds_ShouldListEntriesWhoseMaxIsBelowThirdPlace()
        {
            var rows = new[]
            {
                Row("a", 1, 14, 40), Row("b", 2, 13, 41), Row("c", 3, 13, 38),
                Row("d", 4, 12, 39), Row("e", 5, 8, 12), Row("f", 6, 7, 9)
            };

            TopThreeRule.OutEntryIds(rows).Should().BeEquivalentTo(new[] { "e", "f" });
        }

        [Fact]
        public void OutEntryIds_ShouldUseRankOrder_WhenRowsArriveInAnyOrder()
        {
            var rows = new[]
            {
                Row("f", 6, 7, 9), Row("a", 1, 14, 40), Row("e", 5, 8, 12),
                Row("c", 3, 13, 38), Row("b", 2, 13, 41), Row("d", 4, 12, 39)
            };

            TopThreeRule.OutEntryIds(rows).Should().BeEquivalentTo(new[] { "e", "f" });
        }

        [Fact]
        public void OutEntryIds_ShouldBeEmpty_WhenNothingHasBeenScored()
        {
            var rows = Enumerable.Range(1, 6).Select(i => Row("e" + i, 1, 0, 30)).ToList();

            TopThreeRule.OutEntryIds(rows).Should().BeEmpty();
        }

        [Fact]
        public void OutEntryIds_ShouldBeEmpty_WhenThreeOrFewerEntries()
        {
            TopThreeRule.OutEntryIds(new[] { Row("a", 1, 10, 10), Row("b", 2, 1, 1) }).Should().BeEmpty();
        }
    }
}
