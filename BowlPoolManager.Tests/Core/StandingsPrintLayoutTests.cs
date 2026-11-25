using FluentAssertions;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    // The standings print is one landscape page: a long pool is split into side-by-side blocks, never a second page.
    public class StandingsPrintLayoutTests
    {
        private static List<int> Rows(int n) => Enumerable.Range(1, n).ToList();

        [Fact]
        public void Split_ShouldUseOneBlockWithDetail_WhenPoolIsSmall()
        {
            var layout = StandingsPrintLayout.Split(Rows(30));

            layout.Blocks.Should().HaveCount(1);
            layout.Blocks[0].Should().HaveCount(30);
            layout.ShowDetail.Should().BeTrue();
        }

        [Fact]
        public void Split_ShouldBalanceBlocksInOrder_WhenPoolIsLong()
        {
            var layout = StandingsPrintLayout.Split(Rows(80));

            layout.Blocks.Should().HaveCount(3);
            layout.Blocks.SelectMany(b => b).Should().Equal(Rows(80));
            layout.Blocks.Max(b => b.Count).Should().BeLessThanOrEqualTo(StandingsPrintLayout.MaxRowsPerBlock);
            layout.ShowDetail.Should().BeFalse();
        }

        [Fact]
        public void Split_ShouldReturnOneEmptyBlock_WhenNoEntries()
        {
            var layout = StandingsPrintLayout.Split(new List<int>());

            layout.Blocks.Should().HaveCount(1);
            layout.Blocks[0].Should().BeEmpty();
        }
    }
}
