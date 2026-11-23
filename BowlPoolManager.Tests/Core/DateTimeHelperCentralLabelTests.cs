using FluentAssertions;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>Issue 107: one display helper that always shows Central time with "CT" (BUSINESS-RULES section 7).</summary>
    public class DateTimeHelperCentralLabelTests
    {
        [Fact]
        public void ToCentralLabel_ShouldShowCentralWinterTimeWithCT()
        {
            var utc = new DateTime(2026, 12, 6, 18, 30, 0, DateTimeKind.Utc); // CST, UTC-6

            DateTimeHelper.ToCentralLabel(utc, "MMM d, h:mm tt").Should().Be("Dec 6, 12:30 PM CT");
        }

        [Fact]
        public void ToCentralLabel_ShouldShowCentralSummerTimeWithCT()
        {
            var utc = new DateTime(2026, 7, 15, 17, 0, 0, DateTimeKind.Utc); // CDT, UTC-5

            DateTimeHelper.ToCentralLabel(utc, "MMM d, h:mm tt").Should().Be("Jul 15, 12:00 PM CT");
        }

        [Theory]
        [InlineData(DateTimeKind.Utc)]
        [InlineData(DateTimeKind.Unspecified)]
        public void ToCentralLabel_ShouldTreatUnspecifiedKindAsUtc(DateTimeKind kind)
        {
            var value = new DateTime(2026, 12, 6, 18, 30, 0, kind);

            DateTimeHelper.ToCentralLabel(value, "h:mm tt").Should().Be("12:30 PM CT");
        }

        [Fact]
        public void ToCentralLabel_ShouldConvertALocalKindValueFromItsUtcMoment()
        {
            var local = new DateTime(2026, 12, 6, 18, 30, 0, DateTimeKind.Utc).ToLocalTime();

            DateTimeHelper.ToCentralLabel(local, "h:mm tt").Should().Be("12:30 PM CT");
        }

        [Fact]
        public void ToCentralLabel_ShouldReturnTheFallback_WhenTheValueIsNull()
        {
            DateTimeHelper.ToCentralLabel((DateTime?)null, "g", "Never").Should().Be("Never");
        }

        [Fact]
        public void ToCentralLabel_ShouldFormatANullableValueThatHasOne()
        {
            DateTime? utc = new DateTime(2026, 12, 6, 18, 30, 0, DateTimeKind.Utc);

            DateTimeHelper.ToCentralLabel(utc, "h:mm tt", "Never").Should().Be("12:30 PM CT");
        }
    }
}
