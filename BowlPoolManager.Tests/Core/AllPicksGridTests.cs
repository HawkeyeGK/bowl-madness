using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    // The all-picks print: every entry's pick for every game, as a short team name, marked once it is decided.
    // A pick is out as soon as its team loses any game, so a later playoff pick on an eliminated team is crossed out early.
    public class AllPicksGridTests
    {
        private static BowlGame Final(string id, string home, int homeScore, string away, int awayScore) =>
            new BowlGame { Id = id, TeamHome = home, TeamAway = away, TeamHomeScore = homeScore, TeamAwayScore = awayScore, Status = GameStatus.Final };

        private static BowlGame Open(string id, string home, string away) =>
            new BowlGame { Id = id, TeamHome = home, TeamAway = away, Status = GameStatus.Scheduled };

        [Fact]
        public void Cell_ShouldBeCorrect_WhenThePickWonAFinalGame()
        {
            var g = Final("g", "Utah", 30, "Nebraska", 20);
            var grid = AllPicksGrid.For(new[] { g });

            grid.Cell(g, "Utah").Mark.Should().Be(GridMark.Correct);
            grid.Cell(g, "Nebraska").Mark.Should().Be(GridMark.Out);
        }

        [Fact]
        public void Cell_ShouldBeOut_WhenThePickedTeamLostAnEarlierGame()
        {
            var r1 = Final("r1", "Tulane", 10, "Texas", 24);
            var qf = Open("qf", "Ohio State", "Winner of r1");
            var grid = AllPicksGrid.For(new[] { r1, qf });

            grid.Cell(qf, "Tulane").Mark.Should().Be(GridMark.Out);
            grid.Cell(qf, "Texas").Mark.Should().Be(GridMark.None);
        }

        // Same rule as scoring (BUSINESS-RULES section 4): a Final game without both scores is undecided.
        [Fact]
        public void Cell_ShouldMarkNothing_WhenAFinalGameIsMissingAScore()
        {
            var g = new BowlGame { Id = "g", TeamHome = "Utah", TeamAway = "Nebraska", TeamHomeScore = 21, Status = GameStatus.Final };
            var later = Open("later", "Nebraska", "Iowa");
            var grid = AllPicksGrid.For(new[] { g, later });

            grid.Cell(g, "Utah").Mark.Should().Be(GridMark.None);
            grid.Cell(g, "Nebraska").Mark.Should().Be(GridMark.None);
            grid.Cell(later, "Nebraska").Mark.Should().Be(GridMark.None);
        }

        [Fact]
        public void Cell_ShouldStayCorrect_WhenThePickWonThisGameAndLostALaterOne()
        {
            var qf = Final("qf", "Texas", 30, "Ohio State", 20);
            var sf = Final("sf", "Texas", 10, "Oregon", 31);
            var grid = AllPicksGrid.For(new[] { qf, sf });

            grid.Cell(qf, "Texas").Mark.Should().Be(GridMark.Correct);
            grid.Cell(sf, "Texas").Mark.Should().Be(GridMark.Out);
        }

        [Fact]
        public void Cell_ShouldBeEmpty_WhenThereIsNoPick()
        {
            var g = Open("g", "Utah", "Nebraska");

            AllPicksGrid.For(new[] { g }).Cell(g, null).Should().Be(new GridCell("", GridMark.None));
            AllPicksGrid.For(new[] { g }).Cell(g, " ").Should().Be(new GridCell("", GridMark.None));
        }

        [Fact]
        public void Cell_ShouldIgnoreCase_WhenMatchingTeams()
        {
            var g = Final("g", "Utah", 30, "Nebraska", 20);

            AllPicksGrid.For(new[] { g }).Cell(g, "utah").Mark.Should().Be(GridMark.Correct);
        }

        [Fact]
        public void ShortName_ShouldUseTheTeamsAbbreviation_WhenTheGameCarriesIt()
        {
            var g = Open("g", "Ohio State", "Texas");
            g.HomeTeamInfo = new TeamInfo { School = "Ohio State", Abbreviation = "OSU" };
            var grid = AllPicksGrid.For(new[] { g });

            grid.Cell(g, "Ohio State").Text.Should().Be("OSU");
        }

        [Fact]
        public void ShortName_ShouldFallBackToTheFirstLetters_WhenNoAbbreviationIsKnown()
        {
            var g = Open("g", "Mississippi State", "Wake Forest");
            var grid = AllPicksGrid.For(new[] { g });

            grid.Cell(g, "Mississippi State").Text.Should().Be("MISS");
            grid.Cell(g, "SMU").Text.Should().Be("SMU");
        }

        [Fact]
        public void ShortName_ShouldFindTheAbbreviationFromAnotherGame_WhenThePickIsInALaterRound()
        {
            var r1 = Open("r1", "Tulane", "Texas");
            r1.AwayTeamInfo = new TeamInfo { School = "Texas", Abbreviation = "TEX" };
            var qf = Open("qf", "Ohio State", "TBD");
            var grid = AllPicksGrid.For(new[] { r1, qf });

            grid.Cell(qf, "Texas").Text.Should().Be("TEX");
        }

        [Fact]
        public void Order_ShouldFollowTheStandingsThenName_WhenRanksAreKnown()
        {
            var entries = new[]
            {
                new BracketEntry { Id = "a", PlayerName = "Zed" }, new BracketEntry { Id = "b", PlayerName = "Amy" },
                new BracketEntry { Id = "c", PlayerName = "Bob" }, new BracketEntry { Id = "d", PlayerName = "Cal" },
            };
            var ranks = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2, ["c"] = 2 };

            AllPicksGrid.Order(entries, ranks).Select(e => e.Id).Should().Equal("a", "b", "c", "d");
        }

        [Theory]
        [InlineData(1, 10)]
        [InlineData(30, 9)]
        [InlineData(54, 5)]
        [InlineData(200, 5)]   // never below 5: one page always, however many entries (owner, 2026-11-25)
        public void CellFontPx_ShouldShrinkWithEntryCount_WhenMoreEntriesAreAdded(int entries, int expected)
        {
            AllPicksGrid.CellFontPx(entries).Should().Be(expected);
        }

        [Theory]
        [InlineData(10, 10)]
        [InlineData(34, 10)]
        [InlineData(45, 9)]
        [InlineData(90, 6)]    // never below 6
        public void RowFontPx_ShouldShrinkWithGameCount_WhenMoreGamesAreAdded(int games, int expected)
        {
            AllPicksGrid.RowFontPx(games).Should().Be(expected);
        }
    }
}
