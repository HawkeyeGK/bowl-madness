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

        // The New Pool button passes `new BowlPool()`, whose Id is already a GUID. The page used to treat "Id is empty"
        // as "new pool", so the season default never ran and the Season dropdown opened blank.
        [Fact]
        public void Apply_ShouldSetTheCurrentSeasonOnABrandNewPool_WhenThePoolAlreadyHasAGuidId()
        {
            var current = new Season { Id = "9f6c1c6e-0a55-4c3e-9f55-1d2b7a8c4e10", SeasonId = "2026", IsCurrent = true };
            var pool = new BowlPool();

            NewPoolSeason.Apply(pool, new[] { new Season { Id = "old", SeasonId = "2025" }, current }, fallbackYear: 1999);

            pool.Id.Should().NotBeNullOrEmpty();
            pool.SeasonId.Should().Be(current.Id);
            pool.Season.Should().Be(2026);
        }

        // A new pool starts with every game of its season selected (owner decision); the admin unchecks the ones to leave out.
        // A new pool starts with every game of its season selected (owner decision); the admin unchecks the ones to leave out.
        [Fact]
        public void SelectAllGames_ShouldSelectEveryGameOfTheSeason_WhenTheNewPoolHasNone()
        {
            var pool = new BowlPool();
            var games = new[] { new BowlGame { Id = "g1" }, new BowlGame { Id = "g2" }, new BowlGame { Id = "g3" } };

            NewPoolSeason.SelectAllGames(pool, games);

            pool.GameIds.Should().Equal("g1", "g2", "g3");
        }

        [Fact]
        public void SelectAllGames_ShouldKeepTheAdminsChoice_WhenTheNewPoolAlreadyHasGames()
        {
            var pool = new BowlPool { GameIds = new List<string> { "g2" } };

            NewPoolSeason.SelectAllGames(pool, new[] { new BowlGame { Id = "g1" }, new BowlGame { Id = "g2" } });

            pool.GameIds.Should().Equal("g2");
        }

        [Fact]
        public void Apply_ShouldUseTheFallbackYear_WhenNoSeasonIsCurrent()
        {
            var pool = new BowlPool();

            NewPoolSeason.Apply(pool, new[] { new Season { Id = "old", SeasonId = "2025" } }, fallbackYear: 2026);

            pool.SeasonId.Should().Be("2026");
            pool.Season.Should().Be(2026);
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
