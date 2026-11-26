using FluentAssertions;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    public class PlaceTextTests
    {
        [Theory]
        [InlineData(1, "1st place")]
        [InlineData(2, "2nd place")]
        [InlineData(3, "3rd place")]
        [InlineData(4, "4th place")]
        [InlineData(11, "11th place")]
        [InlineData(12, "12th place")]
        [InlineData(13, "13th place")]
        [InlineData(21, "21st place")]
        [InlineData(22, "22nd place")]
        [InlineData(101, "101st place")]
        [InlineData(111, "111th place")]
        public void For_ShouldWriteTheOrdinal_WhenNobodyShareTheRank(int rank, string expected)
        {
            PlaceText.For(rank, tied: false).Should().Be(expected);
        }

        [Fact]
        public void For_ShouldSayTied_WhenTheRankIsShared()
        {
            PlaceText.For(2, tied: true).Should().Be("tied for 2nd place");
        }
    }
}
