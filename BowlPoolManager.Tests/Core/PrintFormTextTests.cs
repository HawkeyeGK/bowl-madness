using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    // Issues 125 (tiebreaker line names the pool's game) and 126 (header year comes from the season, not the clock).
    public class PrintFormTextTests
    {
        private static BowlGame Game(string id, string bowl, string home, string away) =>
            new BowlGame { Id = id, BowlName = bowl, TeamHome = home, TeamAway = away };

        [Fact]
        public void TieBreakerLine_ShouldNameThePoolsGame_WhenPoolHasATiebreakerGame()
        {
            var pool = new BowlPool { TieBreakerGameId = "g2" };
            var games = new[] { Game("g1", "Rose Bowl", "Ohio State", "Oregon"), Game("g2", "Citrus Bowl", "Iowa", "Tennessee") };

            PrintFormText.TieBreakerLine(pool, games)
                .Should().Be("Tiebreaker: Total points in the Citrus Bowl (Iowa vs Tennessee)");
        }

        [Fact]
        public void TieBreakerLine_ShouldFollowThePool_WhenPoolChanges()
        {
            var games = new[] { Game("g1", "Rose Bowl", "Ohio State", "Oregon"), Game("g2", "Citrus Bowl", "Iowa", "Tennessee") };

            PrintFormText.TieBreakerLine(new BowlPool { TieBreakerGameId = "g1" }, games).Should().Contain("Rose Bowl");
            PrintFormText.TieBreakerLine(new BowlPool { TieBreakerGameId = "g2" }, games).Should().Contain("Citrus Bowl");
        }

        [Fact]
        public void TieBreakerLine_ShouldBeNull_WhenPoolHasNoTiebreakerGame()
        {
            var games = new[] { Game("g1", "Rose Bowl", "Ohio State", "Oregon") };

            PrintFormText.TieBreakerLine(new BowlPool { TieBreakerGameId = "" }, games).Should().BeNull();
        }

        [Fact]
        public void TieBreakerLine_ShouldBeNull_WhenTiebreakerGameIsNotAmongTheGames()
        {
            var games = new[] { Game("g1", "Rose Bowl", "Ohio State", "Oregon") };

            PrintFormText.TieBreakerLine(new BowlPool { TieBreakerGameId = "gone" }, games).Should().BeNull();
            PrintFormText.TieBreakerLine(null, games).Should().BeNull();
        }

        [Fact]
        public void Title_ShouldUseTheSeasonYear_WhenSeasonIsKnown()
        {
            var season = new Season { Id = "guid", SeasonId = "2026" };

            PrintFormText.Title("BOWL MADNESS", new BowlPool { Season = 1999 }, season).Should().Be("BOWL MADNESS 2026");
        }

        [Fact]
        public void Title_ShouldUseThePoolsYear_WhenSeasonIsMissing()
        {
            PrintFormText.Title("BOWL MADNESS", new BowlPool { Season = 2026 }, null).Should().Be("BOWL MADNESS 2026");
        }

        // The form must not read the clock: printed in January 2027 it still says the season's year.
        [Fact]
        public void Title_ShouldLeaveTheYearOut_WhenNeitherSeasonNorPoolHasOne()
        {
            PrintFormText.Title("BOWL MADNESS", new BowlPool { Season = 0 }, new Season { SeasonId = "not a year" })
                .Should().Be("BOWL MADNESS");
        }
    }
}
