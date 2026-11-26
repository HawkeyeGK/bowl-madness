using FluentAssertions;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    // The standings print is one landscape page: a long pool is split into side-by-side blocks, never a second page.
    // The "Coming up" panel takes the room of one block, so it shows only while the rows fit in two blocks.
    public class StandingsPrintLayoutTests
    {
        private static List<int> Rows(int n) => Enumerable.Range(1, n).ToList();

        [Fact]
        public void Split_ShouldUseOneBlockWithRoomForThePanel_WhenPoolIsSmall()
        {
            var layout = StandingsPrintLayout.Split(Rows(20));

            layout.Blocks.Should().HaveCount(1);
            layout.Blocks[0].Should().HaveCount(20);
            layout.HasRoomForPanel.Should().BeTrue();
        }

        [Fact]
        public void Split_ShouldUseTwoBlocksAndKeepThePanel_WhenPoolFillsTwoColumns()
        {
            var layout = StandingsPrintLayout.Split(Rows(35));

            layout.Blocks.Should().HaveCount(2);
            layout.Blocks.SelectMany(b => b).Should().Equal(Rows(35));
            layout.HasRoomForPanel.Should().BeTrue();
        }

        [Fact]
        public void Split_ShouldStayNormalSize_WhenTwoBlocksHoldEveryone()
        {
            var layout = StandingsPrintLayout.Split(Rows(56));

            layout.Blocks.Should().HaveCount(2);
            layout.Compact.Should().BeFalse();
        }

        // Three narrow blocks leave names too little room at normal size, so a long pool gets smaller, taller blocks instead.
        [Fact]
        public void Split_ShouldUseCompactBlocksInOrderAndDropThePanel_WhenPoolIsLong()
        {
            var layout = StandingsPrintLayout.Split(Rows(80));

            layout.Compact.Should().BeTrue();
            layout.Blocks.Should().HaveCount(3);
            layout.Blocks.SelectMany(b => b).Should().Equal(Rows(80));
            layout.Blocks.Max(b => b.Count).Should().BeLessThanOrEqualTo(StandingsPrintLayout.MaxCompactRowsPerBlock);
            layout.HasRoomForPanel.Should().BeFalse();
        }

        [Fact]
        public void Split_ShouldFitThreeCompactBlocks_WhenPoolIsVeryLong()
        {
            var layout = StandingsPrintLayout.Split(Rows(108));

            layout.Blocks.Should().HaveCount(3);
            layout.Blocks.SelectMany(b => b).Should().Equal(Rows(108));
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
