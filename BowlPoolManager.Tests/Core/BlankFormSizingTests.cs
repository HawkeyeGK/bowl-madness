using FluentAssertions;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    // The printed form is one page always: more bowls means a fourth column and smaller type, never a second page or overlapping bowls.
    public class BlankFormSizingTests
    {
        [Theory]
        [InlineData(1, 11)]
        [InlineData(8, 9)]    // last season: 23 bowls in three columns
        [InlineData(9, 8)]    // 34 bowls in four columns
        [InlineData(10, 7)]
        [InlineData(40, 7)]   // never below 7
        public void BowlFontPx_ShouldShrinkWithRowCount_WhenMoreBowlsAreAdded(int rows, int expected)
        {
            BlankFormSizing.BowlFontPx(rows).Should().Be(expected);
        }

        [Theory]
        [InlineData(0, 3)]
        [InlineData(23, 3)]
        [InlineData(27, 3)]
        [InlineData(28, 4)]
        [InlineData(40, 4)]
        public void Columns_ShouldAddAFourthColumn_WhenThreeColumnsWouldNeedMoreThanNineRows(int bowls, int expected)
        {
            BlankFormSizing.Columns(bowls).Should().Be(expected);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(3, 1)]
        [InlineData(4, 2)]
        [InlineData(23, 8)]
        [InlineData(27, 9)]
        [InlineData(34, 9)]
        public void Rows_ShouldFillTheColumns_WhenCountedByBowls(int bowls, int expected)
        {
            BlankFormSizing.Rows(bowls).Should().Be(expected);
        }
    }
}
