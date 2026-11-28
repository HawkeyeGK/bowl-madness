using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>Issue 138: the current-season fallback and the order an entry is looked up across seasons.</summary>
    public class CurrentSeasonTests
    {
        // GUID-like ids chosen so that ordering by Id would pick the oldest season.
        private static Season S(string id, string year, bool current = false) =>
            new() { Id = id, SeasonId = year, Name = year, IsCurrent = current };

        [Fact]
        public void Pick_ShouldReturnTheSeasonMarkedCurrent_WhenOneIsMarked()
        {
            var seasons = new[] { S("f-guid", "2023"), S("a-guid", "2024", current: true), S("b-guid", "2025") };

            CurrentSeason.Pick(seasons)!.Id.Should().Be("a-guid");
        }

        [Fact]
        public void Pick_ShouldReturnTheNewestYear_WhenNoneIsMarkedCurrent()
        {
            var seasons = new[] { S("ffff", "2023"), S("0000", "2025"), S("8888", "2024") };

            CurrentSeason.Pick(seasons)!.SeasonId.Should().Be("2025");
        }

        [Fact]
        public void Pick_ShouldPreferAYearOverText_WhenASeasonIdIsNotANumber()
        {
            var seasons = new[] { S("zzzz", "legacy"), S("0000", "2024") };

            CurrentSeason.Pick(seasons)!.SeasonId.Should().Be("2024");
        }

        [Fact]
        public void Pick_ShouldReturnNull_WhenThereAreNoSeasons()
        {
            CurrentSeason.Pick(Array.Empty<Season>()).Should().BeNull();
        }

        [Fact]
        public void EntryLookupOrder_ShouldTryTheCurrentSeasonFirstThenTheOthersNewestFirst()
        {
            var seasons = new[] { S("s23", "2023"), S("s25", "2025"), S("s24", "2024", current: true) };

            CurrentSeason.EntryLookupOrder(seasons).Should().Equal("s24", "s25", "s23");
        }

        [Fact]
        public void EntryLookupOrder_ShouldListEachSeasonOnce_AndSkipSeasonsWithoutAnId()
        {
            var seasons = new[] { S("s25", "2025", current: true), S("", "2022"), S("s24", "2024") };

            CurrentSeason.EntryLookupOrder(seasons).Should().Equal("s25", "s24");
        }

        [Fact]
        public void EntryLookupOrder_ShouldBeEmpty_WhenThereAreNoSeasons()
        {
            CurrentSeason.EntryLookupOrder(Array.Empty<Season>()).Should().BeEmpty();
        }
    }
}
