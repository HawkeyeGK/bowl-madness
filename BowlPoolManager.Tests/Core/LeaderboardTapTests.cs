using FluentAssertions;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    public class LeaderboardTapTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void For_ShouldShowChoices_WhenNothingIsRemembered(bool poolLocked)
        {
            LeaderboardTap.For(isOwnRow: false, isSignedInOwnEntry: false, hasRememberedRow: false, poolLocked)
                .Should().Be(LeaderboardTapAction.ShowChoices);
        }

        [Fact]
        public void For_ShouldOpenPicks_WhenAnotherEntryIsRememberedAndPoolIsLocked()
        {
            LeaderboardTap.For(isOwnRow: false, isSignedInOwnEntry: false, hasRememberedRow: true, poolLocked: true)
                .Should().Be(LeaderboardTapAction.OpenPicks);
        }

        [Fact]
        public void For_ShouldDoNothing_WhenAnotherEntryIsRememberedBeforeLock()
        {
            LeaderboardTap.For(isOwnRow: false, isSignedInOwnEntry: false, hasRememberedRow: true, poolLocked: false)
                .Should().Be(LeaderboardTapAction.None);
        }

        [Fact]
        public void For_ShouldDoNothing_WhenTheRememberedRowIsTappedBeforeLock()
        {
            LeaderboardTap.For(isOwnRow: true, isSignedInOwnEntry: false, hasRememberedRow: true, poolLocked: false)
                .Should().Be(LeaderboardTapAction.None);
        }

        [Fact]
        public void For_ShouldOpenPicks_WhenTheRememberedRowIsTappedAfterLock()
        {
            LeaderboardTap.For(isOwnRow: true, isSignedInOwnEntry: false, hasRememberedRow: true, poolLocked: true)
                .Should().Be(LeaderboardTapAction.OpenPicks);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void For_ShouldOpenPicks_WhenTheSignedInPlayersOwnRowIsTapped(bool hasRememberedRow, bool poolLocked)
        {
            LeaderboardTap.For(isOwnRow: true, isSignedInOwnEntry: true, hasRememberedRow, poolLocked)
                .Should().Be(LeaderboardTapAction.OpenPicks);
        }
    }
}
