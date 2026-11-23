using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>
    /// Issue 133 / BUSINESS-RULES section 4: a Final game with no winner (a missing score or a tie) is undecided.
    /// It used to be awarded to the away team, so its pickers were paid for a game nobody had won.
    /// </summary>
    public class ScoringEngineFinalWithoutWinnerTests
    {
        private static BowlGame Final(int? home, int? away) => new()
        {
            Id = "g1", TeamHome = "Home", TeamAway = "Away", Status = GameStatus.Final,
            TeamHomeScore = home, TeamAwayScore = away, PointValue = 10, Round = TournamentRound.Standard
        };

        private static BracketEntry Pick(string team) => new()
        {
            Id = "e-" + team, PlayerName = team,
            Picks = new Dictionary<string, string> { ["g1"] = team }
        };

        [Theory]
        [InlineData(null, null)]
        [InlineData(7, 7)]
        [InlineData(null, 7)]
        [InlineData(7, null)]
        public void Calculate_ShouldNotAwardPoints_WhenFinalGameHasNoWinner(int? home, int? away)
        {
            var rows = ScoringEngine.Calculate(new List<BowlGame> { Final(home, away) }, new List<BracketEntry> { Pick("Home"), Pick("Away") });

            rows.Should().OnlyContain(r => r.Score == 0 && r.CorrectPicks == 0);
        }

        [Fact]
        public void Calculate_ShouldKeepMaxPossible_WhenFinalGameHasNoWinner()
        {
            var rows = ScoringEngine.Calculate(new List<BowlGame> { Final(7, 7) }, new List<BracketEntry> { Pick("Home"), Pick("Away") });

            rows.Should().OnlyContain(r => r.MaxPossible == 10, "the game is undecided, so neither pick is lost");
        }

        [Fact]
        public void Calculate_ShouldAwardTheHomeWinner_WhenFinalGameHasScores()
        {
            var rows = ScoringEngine.Calculate(new List<BowlGame> { Final(21, 14) }, new List<BracketEntry> { Pick("Home"), Pick("Away") });

            rows.Single(r => r.Entry.PlayerName == "Home").Score.Should().Be(10);
            rows.Single(r => r.Entry.PlayerName == "Away").Score.Should().Be(0);
        }

        [Fact]
        public void Calculate_ShouldAwardTheAwayWinner_WhenFinalGameHasScores()
        {
            var rows = ScoringEngine.Calculate(new List<BowlGame> { Final(14, 21) }, new List<BracketEntry> { Pick("Home"), Pick("Away") });

            rows.Single(r => r.Entry.PlayerName == "Away").Score.Should().Be(10);
            rows.Single(r => r.Entry.PlayerName == "Home").Score.Should().Be(0);
        }
    }
}
