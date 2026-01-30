using Xunit;
using FluentAssertions;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    public class DateTimeHelperTests
    {
        [Fact]
        public void ToCentral_ShouldConvertUtcToCentralTime()
        {
            // Arrange - January (CST = UTC-6)
            var utc = new DateTime(2025, 1, 15, 18, 0, 0, DateTimeKind.Utc); // 6 PM UTC

            // Act
            var central = DateTimeHelper.ToCentral(utc);

            // Assert - Should be 12 PM Central (6 hours behind)
            central.Hour.Should().Be(12);
            central.Day.Should().Be(15);
        }

        [Fact]
        public void ToCentral_ShouldHandleDaylightSavingTime()
        {
            // Arrange - July (CDT = UTC-5)
            var utc = new DateTime(2025, 7, 15, 17, 0, 0, DateTimeKind.Utc); // 5 PM UTC

            // Act
            var central = DateTimeHelper.ToCentral(utc);

            // Assert - Should be 12 PM Central (5 hours behind during DST)
            central.Hour.Should().Be(12);
            central.Day.Should().Be(15);
        }

        [Fact]
        public void FromCentral_ShouldConvertCentralToUtc()
        {
            // Arrange - January (CST = UTC-6)
            var central = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Unspecified); // 12 PM Central

            // Act
            var utc = DateTimeHelper.FromCentral(central);

            // Assert - Should be 6 PM UTC
            utc.Hour.Should().Be(18);
            utc.Kind.Should().Be(DateTimeKind.Utc);
        }

        [Fact]
        public void FromCentral_ShouldReturnUtcUnchanged_WhenKindIsUtc()
        {
            // Arrange - Already UTC
            var alreadyUtc = new DateTime(2025, 1, 15, 18, 0, 0, DateTimeKind.Utc);

            // Act
            var result = DateTimeHelper.FromCentral(alreadyUtc);

            // Assert - Should return unchanged
            result.Should().Be(alreadyUtc);
        }

        [Fact]
        public void ZoneInfo_ShouldReturnValidTimeZone()
        {
            // Act
            var zoneInfo = DateTimeHelper.ZoneInfo;

            // Assert
            zoneInfo.Should().NotBeNull();
            // Should be either IANA or Windows timezone ID
            (zoneInfo.Id == "America/Chicago" || zoneInfo.Id == "Central Standard Time" || zoneInfo.Id == TimeZoneInfo.Local.Id)
                .Should().BeTrue("ZoneInfo should be Central Time or fallback to Local");
        }

        [Fact]
        public void ToCentral_ShouldHandleMidnightBoundary()
        {
            // Arrange - Midnight UTC on Jan 1
            var utc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Act
            var central = DateTimeHelper.ToCentral(utc);

            // Assert - Should roll back to Dec 31 (previous day)
            central.Day.Should().Be(31);
            central.Month.Should().Be(12);
            central.Year.Should().Be(2024);
        }
    }
}
