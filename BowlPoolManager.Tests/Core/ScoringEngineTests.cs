using Xunit;
using FluentAssertions;
using BowlPoolManager.Core.Helpers;
using BowlPoolManager.Core.Domain;
using System.Collections.Generic;

namespace BowlPoolManager.Tests.Core
{
    public class ScoringEngineTests
    {
        [Fact]
        public void Calculate_ShouldReturnCorrectScore_ForFinalGames()
        {
            // Arrange
            var games = new List<BowlGame>
            {
                new BowlGame { Id = "1", TeamHome = "A", TeamAway = "B", TeamHomeScore = 10, TeamAwayScore = 5, Status = GameStatus.Final, PointValue = 10, Round = PlayoffRound.Standard },
                new BowlGame { Id = "2", TeamHome = "C", TeamAway = "D", TeamHomeScore = 2, TeamAwayScore = 3, Status = GameStatus.Final, PointValue = 5, Round = PlayoffRound.Standard }
            };

            var entry = new BracketEntry
            {
                Id = "e1",
                PlayerName = "Test User",
                Picks = new Dictionary<string, string>
                {
                    { "1", "A" }, // Correct (10 pts)
                    { "2", "C" }  // Incorrect (0 pts)
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
        public void Calculate_ShouldCalculateMaxPossible_WithEliminatedTeams()
        {
            // Arrange
            // Game 1: A beats B (Final). Loser B is eliminated.
            // Game 2: C vs D (Future). neither eliminated yet.
            var games = new List<BowlGame>
            {
                new BowlGame { Id = "1", TeamHome = "A", TeamAway = "B", TeamHomeScore = 20, TeamAwayScore = 10, Status = GameStatus.Final, PointValue = 10 },
                new BowlGame { Id = "2", TeamHome = "C", TeamAway = "D", Status = GameStatus.Scheduled, PointValue = 20 }
            };

            var entry = new BracketEntry
            {
                Id = "e1",
                PlayerName = "Test User",
                Picks = new Dictionary<string, string>
                {
                    { "1", "B" }, // Picked B (Eliminated!) - 0 score, 0 max potential
                    { "2", "C" }  // Picked C (Alive) - 0 score, 20 max potential
                }
            };

            // Act
            var results = ScoringEngine.Calculate(games, new List<BracketEntry> { entry });

            // Assert
            results[0].Score.Should().Be(0);
            
            // MaxPossible should be 20 (for Game 2) because Game 1 is lost/eliminated.
            results[0].MaxPossible.Should().Be(20);
        }

        [Fact]
        public void Calculate_ShouldHandleRedactedEntries_WithoutCrashing()
        {
             // Arrange
            var games = new List<BowlGame>();
            var entry = new BracketEntry
            {
                Id = "e1",
                PlayerName = "Hidden User",
                Picks = null // Redacted
            };

            // Act
            var results = ScoringEngine.Calculate(games, new List<BracketEntry> { entry });

            // Assert
            results.Should().HaveCount(1);
            results[0].Score.Should().Be(0);
            results[0].MaxPossible.Should().Be(0);
        }
    }
}
