using BowlPoolManager.Core.Helpers;
using FluentAssertions;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>The one status line under a page title (times in Central with "CT", BUSINESS-RULES section 7).</summary>
    public class StatusLineTextTests
    {
        // 2026-12-12 17:00 UTC is Saturday 11:00 AM Central (CST).
        private static readonly DateTime LockUtc = new(2026, 12, 12, 17, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void ForPool_ShouldShowTheLockTime_BeforeLock()
        {
            var text = StatusLineText.ForPool(LockUtc.AddDays(-1), LockUtc, null, 0, 26);

            text.Should().Be("Picks lock Sat Dec 12, 11:00 AM CT");
        }

        [Fact]
        public void ForPool_ShouldShowLockTime_WhenNowIsExactlyTheLockDate()
        {
            // BUSINESS-RULES section 3: the lock date itself still accepts changes.
            StatusLineText.ForPool(LockUtc, LockUtc, null, 0, 26).Should().StartWith("Picks lock");
        }

        [Fact]
        public void ForPool_ShouldShowUpdateTimeAndGameCount_AfterLock()
        {
            var updated = new DateTime(2026, 12, 20, 2, 42, 0, DateTimeKind.Utc); // 8:42 PM CST Dec 19
            var text = StatusLineText.ForPool(LockUtc.AddDays(7), LockUtc, updated, 9, 26);

            text.Should().Be("Updated 8:42 PM CT · 9 of 26 games final");
        }

        [Fact]
        public void ForPool_ShouldLeaveOutTheUpdateTime_WhenUnknown()
        {
            StatusLineText.ForPool(LockUtc.AddDays(7), LockUtc, null, 9, 26).Should().Be("9 of 26 games final");
        }

        [Fact]
        public void ForPool_ShouldLeaveOutTheDenominator_WhenTheTotalIsUnknown()
        {
            StatusLineText.ForPool(LockUtc.AddDays(7), LockUtc, null, 9, 0).Should().Be("9 games final");
        }

        [Fact]
        public void ForPool_ShouldSaySingular_WhenOneGameIsFinalAndTotalUnknown()
        {
            StatusLineText.ForPool(LockUtc.AddDays(7), LockUtc, null, 1, 0).Should().Be("1 game final");
        }

        [Fact]
        public void ForPool_ShouldTreatMinValueAsUnknown_ForTheUpdateTime()
        {
            StatusLineText.ForPool(LockUtc.AddDays(7), LockUtc, DateTime.MinValue, 2, 26).Should().Be("2 of 26 games final");
        }

        [Fact]
        public void ForPool_ShouldBeEmpty_WhenThereIsNothingToSay()
        {
            StatusLineText.ForPool(LockUtc.AddDays(7), LockUtc, null, 0, 0).Should().BeEmpty();
        }
    }
}
