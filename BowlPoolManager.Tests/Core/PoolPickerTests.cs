using BowlPoolManager.Core.Helpers;
using FluentAssertions;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>Which pool a page shows first: address, then saved choice, then newest entry, then first pool.</summary>
    public class PoolPickerTests
    {
        private static readonly string[] Pools = { "family", "office", "work" };

        [Fact]
        public void Pick_ShouldReturnNull_WhenThereAreNoPools()
        {
            PoolPicker.Pick(Array.Empty<string>(), "family", "office", "work").Should().BeNull();
        }

        [Fact]
        public void Pick_ShouldUseTheAddress_BeforeEverythingElse()
        {
            PoolPicker.Pick(Pools, "work", "office", "family").Should().Be("work");
        }

        [Fact]
        public void Pick_ShouldFallBackToTheFirstPool_WhenTheAddressNamesAnUnknownPool()
        {
            // Matches the old Leaderboard: a bad ?poolId= does not fall through to the saved choice.
            PoolPicker.Pick(Pools, "gone", "office", "work").Should().Be("family");
        }

        [Fact]
        public void Pick_ShouldUseTheSavedChoice_WhenThereIsNoAddress()
        {
            PoolPicker.Pick(Pools, null, "office", "work").Should().Be("office");
        }

        [Fact]
        public void Pick_ShouldUseTheNewestEntrysPool_WhenTheSavedChoiceIsUnknown()
        {
            PoolPicker.Pick(Pools, null, "archived-pool", "work").Should().Be("work");
            PoolPicker.Pick(Pools, null, null, "work").Should().Be("work");
        }

        [Fact]
        public void Pick_ShouldUseTheFirstPool_WhenNothingElseMatches()
        {
            PoolPicker.Pick(Pools, "", "", "").Should().Be("family");
            PoolPicker.Pick(Pools, null, "x", "y").Should().Be("family");
        }
    }
}
