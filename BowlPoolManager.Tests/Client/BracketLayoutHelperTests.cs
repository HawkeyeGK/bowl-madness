using FluentAssertions;
using BowlPoolManager.Client.Helpers;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Client
{
    public class BracketLayoutHelperTests
    {
        // ── GetSeedMatchupOrder ──────────────────────────────────────────────────

        [Theory]
        [InlineData("1v16", 0)]
        [InlineData("8v9",  1)]
        [InlineData("5v12", 2)]
        [InlineData("4v13", 3)]
        [InlineData("6v11", 4)]
        [InlineData("3v14", 5)]
        [InlineData("7v10", 6)]
        [InlineData("2v15", 7)]
        public void GetSeedMatchupOrder_ShouldReturnCorrectPosition_ForKnownMatchups(string matchup, int expected)
        {
            BracketLayoutHelper.GetSeedMatchupOrder(matchup).Should().Be(expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("unknown")]
        [InlineData("11v11")]
        [InlineData("16v16")]
        public void GetSeedMatchupOrder_ShouldReturn99_ForUnknownMatchups(string? matchup)
        {
            BracketLayoutHelper.GetSeedMatchupOrder(matchup).Should().Be(99);
        }

        [Fact]
        public void GetSeedMatchupOrder_ShouldProduceTotallyOrderedList_ForAllKnownMatchups()
        {
            var matchups = new[] { "1v16", "8v9", "5v12", "4v13", "6v11", "3v14", "7v10", "2v15" };
            var orders = matchups.Select(BracketLayoutHelper.GetSeedMatchupOrder).ToList();

            orders.Should().BeInAscendingOrder();
            orders.Distinct().Should().HaveSameCount(orders); // no ties
        }

        // ── OrderByFeeder ────────────────────────────────────────────────────────

        [Fact]
        public void OrderByFeeder_ShouldOrderR32Games_ByTheirR64FeederPosition()
        {
            // R64 ordered top-to-bottom: g1(1v16), g2(8v9), g3(5v12), g4(4v13) — first 4 of a region
            var r32a = Game("r32a");  // fed by g1 and g2
            var r32b = Game("r32b");  // fed by g3 and g4

            var r64g1 = Game("g1", nextGameId: "r32a", seedMatchup: "1v16");
            var r64g2 = Game("g2", nextGameId: "r32a", seedMatchup: "8v9");
            var r64g3 = Game("g3", nextGameId: "r32b", seedMatchup: "5v12");
            var r64g4 = Game("g4", nextGameId: "r32b", seedMatchup: "4v13");

            // previousRound already ordered by seed matchup (as the component does)
            var r64Ordered = new List<HoopsGame> { r64g1, r64g2, r64g3, r64g4 };

            var result = BracketLayoutHelper.OrderByFeeder(new[] { r32b, r32a }, r64Ordered);

            result.Should().Equal(r32a, r32b);
        }

        [Fact]
        public void OrderByFeeder_ShouldPreserveRelativeOrder_WhenMultipleGamesShareSameFeederIndex()
        {
            // Two R32 games both fed by index-0 game (degenerate case — should not happen in valid data)
            var r32a = Game("r32a");
            var r32b = Game("r32b");
            var r64g = Game("g1", nextGameId: "r32a");

            var r64Ordered = new List<HoopsGame> { r64g };

            // r32b has no feeder → sorts last (99)
            var result = BracketLayoutHelper.OrderByFeeder(new[] { r32b, r32a }, r64Ordered);

            result.Should().Equal(r32a, r32b);
        }

        [Fact]
        public void OrderByFeeder_ShouldReturnEmptyList_WhenInputIsEmpty()
        {
            var result = BracketLayoutHelper.OrderByFeeder(Enumerable.Empty<HoopsGame>(), new List<HoopsGame>());

            result.Should().BeEmpty();
        }

        [Fact]
        public void OrderByFeeder_ShouldSortGamesWithNoFeeder_ToTheEnd()
        {
            var r32a = Game("r32a");  // has feeder at index 0
            var r32b = Game("r32b");  // no feeder in previous round
            var r64g = Game("g1", nextGameId: "r32a");

            var result = BracketLayoutHelper.OrderByFeeder(new[] { r32b, r32a }, new List<HoopsGame> { r64g });

            result.First().Id.Should().Be("r32a");
            result.Last().Id.Should().Be("r32b");
        }

        // ── CascadeClear ─────────────────────────────────────────────────────────

        [Fact]
        public void CascadeClear_ShouldClearDirectDownstreamPick_WhenItMatchesOldTeam()
        {
            var gameA = Game("a", nextGameId: "b");
            var gameB = Game("b");
            var allGames = new List<HoopsGame> { gameA, gameB };

            var picks = new Dictionary<string, string>
            {
                ["a"] = "Duke",
                ["b"] = "Duke"
            };

            BracketLayoutHelper.CascadeClear("a", "Duke", picks, allGames);

            picks.Should().NotContainKey("b");
            picks.Should().ContainKey("a"); // source pick is untouched by CascadeClear
        }

        [Fact]
        public void CascadeClear_ShouldCascadeAcrossMultipleLevels()
        {
            var gameA = Game("a", nextGameId: "b");
            var gameB = Game("b", nextGameId: "c");
            var gameC = Game("c", nextGameId: "d");
            var gameD = Game("d");
            var allGames = new List<HoopsGame> { gameA, gameB, gameC, gameD };

            var picks = new Dictionary<string, string>
            {
                ["a"] = "Duke",
                ["b"] = "Duke",
                ["c"] = "Duke",
                ["d"] = "Duke"
            };

            BracketLayoutHelper.CascadeClear("a", "Duke", picks, allGames);

            picks.Should().ContainKey("a");
            picks.Should().NotContainKey("b");
            picks.Should().NotContainKey("c");
            picks.Should().NotContainKey("d");
        }

        [Fact]
        public void CascadeClear_ShouldStop_WhenDownstreamPickIsDifferentTeam()
        {
            var gameA = Game("a", nextGameId: "b");
            var gameB = Game("b", nextGameId: "c");
            var gameC = Game("c");
            var allGames = new List<HoopsGame> { gameA, gameB, gameC };

            var picks = new Dictionary<string, string>
            {
                ["a"] = "Duke",
                ["b"] = "UNC",   // different team — chain stops here
                ["c"] = "UNC"
            };

            BracketLayoutHelper.CascadeClear("a", "Duke", picks, allGames);

            picks.Should().ContainKey("b"); // not cleared — it was UNC, not Duke
            picks.Should().ContainKey("c");
        }

        [Fact]
        public void CascadeClear_ShouldBeNoOp_WhenDownstreamGameHasNoPick()
        {
            var gameA = Game("a", nextGameId: "b");
            var gameB = Game("b");
            var allGames = new List<HoopsGame> { gameA, gameB };

            var picks = new Dictionary<string, string> { ["a"] = "Duke" };

            BracketLayoutHelper.CascadeClear("a", "Duke", picks, allGames);

            picks.Should().HaveCount(1).And.ContainKey("a");
        }

        [Fact]
        public void CascadeClear_ShouldBeNoOp_WhenFromGameHasNoNextGameId()
        {
            var gameA = Game("a"); // no NextGameId
            var allGames = new List<HoopsGame> { gameA };

            var picks = new Dictionary<string, string> { ["a"] = "Duke" };

            BracketLayoutHelper.CascadeClear("a", "Duke", picks, allGames);

            picks.Should().HaveCount(1);
        }

        [Fact]
        public void CascadeClear_ShouldBeNoOp_WhenFromGameIdNotFound()
        {
            var allGames = new List<HoopsGame> { Game("a") };
            var picks = new Dictionary<string, string> { ["a"] = "Duke" };

            BracketLayoutHelper.CascadeClear("nonexistent", "Duke", picks, allGames);

            picks.Should().HaveCount(1);
        }

        [Fact]
        public void CascadeClear_ShouldBeCaseInsensitive_WhenComparingTeamNames()
        {
            var gameA = Game("a", nextGameId: "b");
            var gameB = Game("b");
            var allGames = new List<HoopsGame> { gameA, gameB };

            var picks = new Dictionary<string, string>
            {
                ["a"] = "Duke",
                ["b"] = "duke"  // lowercase — should still match
            };

            BracketLayoutHelper.CascadeClear("a", "Duke", picks, allGames);

            picks.Should().NotContainKey("b");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static HoopsGame Game(string id, string? nextGameId = null, string? seedMatchup = null) =>
            new HoopsGame
            {
                Id = id,
                NextGameId = nextGameId,
                SeedMatchup = seedMatchup,
                SeasonId = "test"
            };
    }
}
