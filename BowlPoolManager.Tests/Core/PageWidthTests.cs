using BowlPoolManager.Core.Helpers;
using FluentAssertions;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>One page-width rule for the site: header width (1240 px) everywhere, full width only for big grids.</summary>
    public class PageWidthTests
    {
        [Theory]
        [InlineData("what-if")]
        [InlineData("hoops/what-if")]
        [InlineData("admin/dashboard")]
        [InlineData("admin/entries")]
        [InlineData("admin")]
        [InlineData("/admin/components")]
        [InlineData("ADMIN/Users")]
        [InlineData("what-if?poolId=abc")]
        [InlineData("what-if/")]
        [InlineData("admin/entries#top")]
        public void IsWide_ShouldBeTrue_ForBigGridsAndAdminPages(string path)
        {
            PageWidth.IsWide(path).Should().BeTrue();
        }

        [Theory]
        [InlineData("")]
        [InlineData("/")]
        [InlineData("leaderboard")]
        [InlineData("results")]
        [InlineData("mypicks")]
        [InlineData("entry/abc123")]
        [InlineData("archives")]
        [InlineData("login")]
        [InlineData("print-bracket")]
        [InlineData("hoops/leaderboard")]
        [InlineData("hoops/mypicks")]
        [InlineData("hoops/scoreboard")]
        [InlineData("hoops/archives")]
        [InlineData("administration")]
        [InlineData("what-if-not")]
        [InlineData("hoops/admin")]
        public void IsWide_ShouldBeFalse_ForEveryOtherPage(string path)
        {
            PageWidth.IsWide(path).Should().BeFalse();
        }

        [Fact]
        public void IsWide_ShouldBeFalse_WhenThePathIsNull()
        {
            PageWidth.IsWide(null).Should().BeFalse();
        }
    }
}
