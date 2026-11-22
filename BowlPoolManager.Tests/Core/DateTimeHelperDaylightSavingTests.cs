using FluentAssertions;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>
    /// Central time around the daylight-saving changes (2026-03-08 and 2026-11-01). Release 1.1 group D
    /// (#107) shows pick deadlines in Central time, so these pin the conversions before that work starts.
    /// </summary>
    public class DateTimeHelperDaylightSavingTests
    {
        private static DateTime Utc(int y, int mo, int d, int h, int mi, int s = 0) =>
            new(y, mo, d, h, mi, s, DateTimeKind.Utc);

        private static DateTime Central(int y, int mo, int d, int h, int mi, int s = 0) =>
            new(y, mo, d, h, mi, s, DateTimeKind.Unspecified);

        [Fact]
        public void ToCentral_ShouldUseStandardTime_WhenOneSecondBeforeSpringForward()
        {
            DateTimeHelper.ToCentral(Utc(2026, 3, 8, 7, 59, 59)).Should().Be(Central(2026, 3, 8, 1, 59, 59));
        }

        [Fact]
        public void ToCentral_ShouldSkipToThreeAm_WhenClocksSpringForward()
        {
            DateTimeHelper.ToCentral(Utc(2026, 3, 8, 8, 0, 0)).Should().Be(Central(2026, 3, 8, 3, 0, 0));
        }

        [Fact]
        public void ToCentral_ShouldUseDaylightTime_WhenOneSecondBeforeFallBack()
        {
            DateTimeHelper.ToCentral(Utc(2026, 11, 1, 6, 59, 59)).Should().Be(Central(2026, 11, 1, 1, 59, 59));
        }

        [Fact]
        public void ToCentral_ShouldRepeatOneAm_WhenClocksFallBack()
        {
            // 01:30 happens twice on 2026-11-01: 06:30 UTC (CDT) and 07:30 UTC (CST).
            DateTimeHelper.ToCentral(Utc(2026, 11, 1, 6, 30, 0)).Should().Be(Central(2026, 11, 1, 1, 30, 0));
            DateTimeHelper.ToCentral(Utc(2026, 11, 1, 7, 30, 0)).Should().Be(Central(2026, 11, 1, 1, 30, 0));
        }

        [Fact]
        public void FromCentral_ShouldPickStandardTime_WhenLocalTimeIsAmbiguous()
        {
            // 01:30 on 2026-11-01 is ambiguous; the helper (TimeZoneInfo) takes the standard-time reading.
            DateTimeHelper.FromCentral(Central(2026, 11, 1, 1, 30, 0)).Should().Be(Utc(2026, 11, 1, 7, 30, 0));
        }

        [Theory]
        [InlineData(2026, 11, 1, 0, 30, 5, 30)]   // before the repeated hour: still daylight time
        [InlineData(2026, 11, 1, 2, 30, 8, 30)]   // after: standard time
        [InlineData(2026, 3, 8, 1, 30, 7, 30)]    // before the skipped hour: standard time
        [InlineData(2026, 3, 8, 3, 30, 8, 30)]    // after: daylight time
        public void FromCentral_ShouldApplyTheOffsetInForce_WhenTimeIsNotAmbiguous(int y, int mo, int d, int h, int mi, int utcH, int utcMi)
        {
            DateTimeHelper.FromCentral(Central(y, mo, d, h, mi)).Should().Be(Utc(y, mo, d, utcH, utcMi));
        }

        [Fact]
        public void FromCentral_ShouldReturnInputUnchanged_WhenKindIsUtcOnADaylightSavingDay()
        {
            var utc = Utc(2026, 11, 1, 6, 30, 0);

            DateTimeHelper.FromCentral(utc).Should().Be(utc);
        }

        [Theory]
        [InlineData(2026, 3, 8, 12, 0)]
        [InlineData(2026, 11, 1, 12, 0)]
        [InlineData(2026, 12, 19, 18, 30)]
        [InlineData(2027, 7, 4, 9, 15)]
        public void ToCentral_ShouldReturnTheSameLocalTime_WhenRoundTrippedThroughFromCentral(int y, int mo, int d, int h, int mi)
        {
            var central = Central(y, mo, d, h, mi);

            DateTimeHelper.ToCentral(DateTimeHelper.FromCentral(central)).Should().Be(central);
        }

        [Fact]
        public void ToCentral_ShouldLeaveCentralOffsetOfMinusSixInWinter_AndMinusFiveInSummer()
        {
            (Utc(2026, 12, 19, 18, 0, 0) - DateTimeHelper.ToCentral(Utc(2026, 12, 19, 18, 0, 0))).Should().Be(TimeSpan.FromHours(6));
            (Utc(2026, 7, 1, 18, 0, 0) - DateTimeHelper.ToCentral(Utc(2026, 7, 1, 18, 0, 0))).Should().Be(TimeSpan.FromHours(5));
        }
    }
}
