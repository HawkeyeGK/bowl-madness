using FluentAssertions;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    public class PickVisibilityTests
    {
        [Theory]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void OnPublicPage_ShouldShowPicks_WhenThePoolIsLocked(bool locked, bool own)
        {
            PickVisibility.OnPublicPage(locked, own).Should().BeTrue();
        }

        [Fact]
        public void OnPublicPage_ShouldShowPicks_WhenTheEntryIsTheSignedInPlayersOwnBeforeLock()
        {
            PickVisibility.OnPublicPage(poolLocked: false, isViewersOwnEntry: true).Should().BeTrue();
        }

        [Fact]
        public void OnPublicPage_ShouldHidePicks_WhenTheEntryIsSomeoneElsesBeforeLock()
        {
            // Even for an Admin: before lock, others' picks are seen only in the admin section (owner decision 2026-11-25).
            PickVisibility.OnPublicPage(poolLocked: false, isViewersOwnEntry: false).Should().BeFalse();
        }
    }
}
