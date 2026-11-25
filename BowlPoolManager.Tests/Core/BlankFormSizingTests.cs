using FluentAssertions;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    // The blank form is one page always: more bowls means smaller type, never a second page or cut-off teams.
    public class BlankFormSizingTests
    {
        [Theory]
        [InlineData(1, 11)]
        [InlineData(8, 11)]   // last season: 23 bowls in three columns
        [InlineData(11, 9)]
        [InlineData(16, 7)]
        [InlineData(40, 7)]   // never below 7
        public void TeamFontPx_ShouldShrinkWithRowCount_WhenMoreBowlsAreAdded(int rows, int expected)
        {
            BlankFormSizing.TeamFontPx(rows).Should().Be(expected);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(3, 1)]
        [InlineData(4, 2)]
        [InlineData(23, 8)]
        public void Rows_ShouldFillThreeColumns_WhenCountedByBowls(int bowls, int expected)
        {
            BlankFormSizing.Rows(bowls).Should().Be(expected);
        }
    }
}
