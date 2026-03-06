using Xunit;
using FluentAssertions;
using BowlPoolManager.Core.Helpers;
using BowlPoolManager.Core.Domain;
using System.Collections.Generic;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>
    /// Tests for the ScoringEngine.Calculate(List&lt;IScorable&gt;, ...) overload using HoopsGame
    /// objects.  The List&lt;BowlGame&gt; overload is already covered by ScoringEngineTests; these
    /// tests exercise the generic IScorable path with a different concrete type to confirm the
    /// polymorphic overload works correctly.
    /// </summary>
    public class ScoringEngineIScorableTests
    {
        // Helper: create a finished HoopsGame where the home team won.
        private static HoopsGame FinalGame(string id, string home, string away, int homeScore, int awayScore, int pointValue, TournamentRound round = TournamentRound.RoundOf64)
            => new HoopsGame
            {
                Id = id,
                TeamHome = home,
                TeamAway = away,
                TeamHomeScore = homeScore,
                TeamAwayScore = awayScore,
                Status = GameStatus.Final,
                PointValue = pointValue,
                Round = round
            };

        private static HoopsGame ScheduledGame(string id, string home, string away, int pointValue, TournamentRound round = TournamentRound.RoundOf64)
            => new HoopsGame
            {
                Id = id,
                TeamHome = home,
                TeamAway = away,
                Status = GameStatus.Scheduled,
                PointValue = pointValue,
                Round = round
            };

        [Fact]
        public void Calculate_ShouldReturnCorrectScore_WhenGivenHoopsGamesViaIScorableOverload()
        {
            // Arrange
            var games = new List<IScorable>
            {
                FinalGame("g1", "Duke", "UNC",  80, 70, pointValue: 10),   // Duke wins
                FinalGame("g2", "Kansas", "Iowa", 60, 75, pointValue: 10)  // Iowa wins
            };

            var entry = new BracketEntry
            {
                Id = "e1",
                PlayerName = "Test",
                Picks = new Dictionary<string, string>
                {
                    { "g1", "Duke" },   // Correct (10 pts)
                    { "g2", "Kansas" }  // Incorrect (0 pts)
                }
            };

            // Act
            var results = ScoringEngine.Calculate(games, new List<BracketEntry> { entry });

            // Assert
            results.Should().HaveCount(1);
            results[0].Score.Should().Be(10);
            results[0].CorrectPicks.Should().Be(1);
        }

        [Fact]
        public void Calculate_ShouldCalculateMaxPossible_WithHoopsGamesAndEliminatedTeam()
        {
            // Arrange — Team B loses game 1 (eliminated), game 2 is future.
            var games = new List<IScorable>
            {
                FinalGame("g1", "TeamA", "TeamB", 85, 70, pointValue: 10),
                ScheduledGame("g2", "TeamC", "TeamD", pointValue: 20)
            };

            var entry = new BracketEntry
            {
                Id = "e1",
                PlayerName = "Test",
                Picks = new Dictionary<string, string>
                {
                    { "g1", "TeamB" }, // Eliminated — 0 score, 0 max potential
                    { "g2", "TeamC" }  // Alive — 0 score, 20 max potential
                }
            };

            // Act
            var results = ScoringEngine.Calculate(games, new List<BracketEntry> { entry });

            // Assert
            results[0].Score.Should().Be(0);
            results[0].MaxPossible.Should().Be(20);
        }

        [Fact]
        public void Calculate_ShouldHandleHoopsGameWithZeroPointValue()
        {
            // HoopsGame.PointValue defaults to 0 — a correct pick of such a game
            // should contribute 0 to score but still be counted as a correct pick.
            var games = new List<IScorable>
            {
                FinalGame("g1", "Duke", "UNC", 70, 65, pointValue: 0)
            };

            var entry = new BracketEntry
            {
                Id = "e1",
                PlayerName = "Test",
                Picks = new Dictionary<string, string> { { "g1", "Duke" } } // Correct
            };

            var results = ScoringEngine.Calculate(games, new List<BracketEntry> { entry });

            results[0].Score.Should().Be(0);
            results[0].CorrectPicks.Should().Be(1);
            results[0].MaxPossible.Should().Be(0);
        }

        [Fact]
        public void Calculate_ShouldHandleMixedIScorable_BowlGameAndHoopsGame()
        {
            // The IScorable overload accepts any mix of implementations.
            // One BowlGame and one HoopsGame in the same list must both score correctly.
            var bowl = new BowlGame
            {
                Id = "bowl1",
                TeamHome = "Ohio State",
                TeamAway = "Notre Dame",
                TeamHomeScore = 28,
                TeamAwayScore = 14,
                Status = GameStatus.Final,
                PointValue = 5,
                Round = TournamentRound.Standard
            };

            var hoops = FinalGame("hoops1", "Duke", "UNC", 80, 70, pointValue: 8);

            var games = new List<IScorable> { bowl, hoops };

            var entry = new BracketEntry
            {
                Id = "e1",
                PlayerName = "Mixed",
                Picks = new Dictionary<string, string>
                {
                    { "bowl1",  "Ohio State" }, // Correct — 5 pts
                    { "hoops1", "Duke" }        // Correct — 8 pts
                }
            };

            var results = ScoringEngine.Calculate(games, new List<BracketEntry> { entry });

            results[0].Score.Should().Be(13);  // 5 + 8
            results[0].CorrectPicks.Should().Be(2);
        }

        [Fact]
        public void Calculate_ShouldHandleCaseInsensitiveTeamMatching_ForHoopsGames()
        {
            var games = new List<IScorable>
            {
                FinalGame("g1", "Duke Blue Devils", "UNC Tar Heels", 80, 70, pointValue: 10)
            };

            var entry = new BracketEntry
            {
                Id = "e1",
                PlayerName = "Test",
                Picks = new Dictionary<string, string> { { "g1", "DUKE BLUE DEVILS" } }
            };

            var results = ScoringEngine.Calculate(games, new List<BracketEntry> { entry });

            results[0].Score.Should().Be(10);
            results[0].CorrectPicks.Should().Be(1);
        }

        [Fact]
        public void Calculate_ShouldHandleEmptyIScorableList()
        {
            var games = new List<IScorable>();
            var entries = new List<BracketEntry>
            {
                new BracketEntry { Id = "e1", PlayerName = "Test", Picks = new Dictionary<string, string>() }
            };

            var results = ScoringEngine.Calculate(games, entries);

            results.Should().HaveCount(1);
            results[0].Score.Should().Be(0);
            results[0].MaxPossible.Should().Be(0);
            results[0].CorrectPicks.Should().Be(0);
        }

        [Fact]
        public void Calculate_BowlGameOverload_ShouldProduceSameResult_AsIScorableOverload()
        {
            // Regression: the BowlGame overload delegates to the IScorable overload.
            // Both must return identical results for the same input.
            var bowlGames = new List<BowlGame>
            {
                new BowlGame { Id = "g1", TeamHome = "A", TeamAway = "B", TeamHomeScore = 10, TeamAwayScore = 5, Status = GameStatus.Final, PointValue = 7, Round = TournamentRound.Standard }
            };

            var iScorableGames = bowlGames.Cast<IScorable>().ToList();

            var entry = new BracketEntry
            {
                Id = "e1",
                PlayerName = "Test",
                Picks = new Dictionary<string, string> { { "g1", "A" } }
            };

            var resultBowl     = ScoringEngine.Calculate(bowlGames,     new List<BracketEntry> { entry });
            var resultIScorable = ScoringEngine.Calculate(iScorableGames, new List<BracketEntry> { entry });

            resultBowl[0].Score.Should().Be(resultIScorable[0].Score);
            resultBowl[0].CorrectPicks.Should().Be(resultIScorable[0].CorrectPicks);
            resultBowl[0].MaxPossible.Should().Be(resultIScorable[0].MaxPossible);
        }
    }
}
