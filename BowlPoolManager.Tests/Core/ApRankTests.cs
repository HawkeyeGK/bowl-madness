using FluentAssertions;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    public class ApRankTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData(1)]
        [InlineData(15)]
        [InlineData(25)]
        public void IsValid_ShouldBeTrue_WhenRankIsMissingOrInTheTop25(int? rank)
        {
            ApRank.IsValid(rank).Should().BeTrue();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        [InlineData(26)]
        [InlineData(100)]
        public void IsValid_ShouldBeFalse_WhenRankIsOutsideTheTop25(int rank)
        {
            ApRank.IsValid(rank).Should().BeFalse();
        }

        [Fact]
        public void Label_ShouldPutTheRankBeforeTheName_WhenTheTeamIsRanked()
        {
            ApRank.Label("Utah", 15).Should().Be("#15 Utah");
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        public void Label_ShouldBeTheNameOnly_WhenTheTeamIsNotRanked(int? rank)
        {
            ApRank.Label("Utah", rank).Should().Be("Utah");
        }
    }
}
