using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    // Regression tests for issue 129: New Pool crashed with a FormatException because the year was parsed from the season's GUID.
    public class NewPoolSeasonTests
    {
        [Fact]
        public void For_ShouldTakeYearFromSeasonIdAndLinkFromId_WhenCurrentSeasonIdIsAGuid()
        {
            var current = new Season { Id = "9f6c1c6e-0a55-4c3e-9f55-1d2b7a8c4e10", SeasonId = "2025", IsCurrent = true };
            var other = new Season { Id = Guid.NewGuid().ToString(), SeasonId = "2024", IsCurrent = false };

            var result = NewPoolSeason.For(new[] { other, current }, fallbackYear: 1999);

            result.SeasonId.Should().Be(current.Id);
            result.Year.Should().Be(2025);
        }

        [Fact]
        public void For_ShouldUseFallbackYear_WhenNoSeasonIsCurrent()
        {
            var result = NewPoolSeason.For(new[] { new Season { SeasonId = "2024" } }, fallbackYear: 2026);

            result.SeasonId.Should().Be("2026");
            result.Year.Should().Be(2026);
        }
    }
}
