using Xunit;
using FluentAssertions;
using BowlPoolManager.Core.Helpers;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Core
{
    public class WhatIfScoringEngineTests
    {
        [Fact]
        public void Calculate_ShouldApplySimulatedWinners_OverRealResults()
        {
            // Arrange
            // Game 1 is Final: A beat B in real life.
            // We SIMULATE that B actually won.
            var games = new List<BowlGame>
            {
                new BowlGame { Id = "g1", TeamHome = "A", TeamAway = "B", TeamHomeScore = 20, TeamAwayScore = 10, Status = GameStatus.Final, PointValue = 10 }
            };

            var entry = new BracketEntry
            {
                Id = "e1",
                PlayerName = "Test",
                Picks = new Dictionary<string, string> { { "g1", "B" } } // Picked B (lost in reality, but we'll simulate B winning)
            };

            var simulated = new Dictionary<string, string> { { "g1", "B" } }; // Simulate B wins

            // Act
            var results = WhatIfScoringEngine.Calculate(games, new List<BracketEntry> { entry }, simulated);

            // Assert
            results[0].Score.Should().Be(10); // Should get 10 points because simulation overrides reality
        }

        [Fact]
        public void Calculate_ShouldUseRealResult_WhenNoSimulation()
        {
            // Arrange
            var games = new List<BowlGame>
            {
                new BowlGame { Id = "g1", TeamHome = "A", TeamAway = "B", TeamHomeScore = 20, TeamAwayScore = 10, Status = GameStatus.Final, PointValue = 10 }
            };

            var entry = new BracketEntry
            {
                Id = "e1",
                PlayerName = "Test",
                Picks = new Dictionary<string, string> { { "g1", "A" } } // Picked A (won in reality)
            };

            var simulated = new Dictionary<string, string>(); // No simulation

            // Act
            var results = WhatIfScoringEngine.Calculate(games, new List<BracketEntry> { entry }, simulated);

            // Assert
            results[0].Score.Should().Be(10);
        }

        [Fact]
        public void Calculate_ShouldEliminateTeams_FromMaxPossible()
        {
            // Arrange
            // Game 1: A vs B (Final, A wins) -> B eliminated
            // Game 2: B vs C (Scheduled) -> If I picked B, max possible = 0 for this game
            var games = new List<BowlGame>
            {
                new BowlGame { Id = "g1", TeamHome = "A", TeamAway = "B", TeamHomeScore = 30, TeamAwayScore = 10, Status = GameStatus.Final, PointValue = 10 },
                new BowlGame { Id = "g2", TeamHome = "B", TeamAway = "C", Status = GameStatus.Scheduled, PointValue = 20 }
            };

            var entry = new BracketEntry
            {
                Id = "e1",
                PlayerName = "Test",
                Picks = new Dictionary<string, string> 
                { 
                    { "g1", "B" }, // Wrong pick, B lost
                    { "g2", "B" }  // B is eliminated, should NOT count toward max
                }
            };

            var simulated = new Dictionary<string, string>();

            // Act
            var results = WhatIfScoringEngine.Calculate(games, new List<BracketEntry> { entry }, simulated);

            // Assert
            results[0].Score.Should().Be(0);
            results[0].MaxPossible.Should().Be(0); // B is eliminated, so game 2 can't help
        }

        [Fact]
        public void Calculate_ShouldRankPlayersCorrectly()
        {
            // Arrange
            var games = new List<BowlGame>
            {
                new BowlGame { Id = "g1", TeamHome = "A", TeamAway = "B", TeamHomeScore = 20, TeamAwayScore = 10, Status = GameStatus.Final, PointValue = 10 }
            };

            var entries = new List<BracketEntry>
            {
                new BracketEntry { Id = "e1", PlayerName = "Alice", Picks = new Dictionary<string, string> { { "g1", "A" } } }, // Correct
                new BracketEntry { Id = "e2", PlayerName = "Bob", Picks = new Dictionary<string, string> { { "g1", "B" } } }    // Wrong
            };

            var simulated = new Dictionary<string, string>();

            // Act
            var results = WhatIfScoringEngine.Calculate(games, entries, simulated);

            // Assert
            results[0].Entry.PlayerName.Should().Be("Alice");
            results[0].Rank.Should().Be(1);
            results[1].Entry.PlayerName.Should().Be("Bob");
            results[1].Rank.Should().Be(2);
        }

        #region Edge Case Tests

        [Fact]
        public void Calculate_ShouldSkipEntriesWithNullPicks()
        {
            // Arrange
            var games = new List<BowlGame>
            {
                new BowlGame { Id = "g1", TeamHome = "A", TeamAway = "B", TeamHomeScore = 10, TeamAwayScore = 5, Status = GameStatus.Final, PointValue = 10 }
            };

            var entries = new List<BracketEntry>
            {
                new BracketEntry { Id = "e1", PlayerName = "Normal", Picks = new Dictionary<string, string> { { "g1", "A" } } },
                new BracketEntry { Id = "e2", PlayerName = "Redacted", Picks = null } // Should be skipped
            };

            // Act
            var results = WhatIfScoringEngine.Calculate(games, entries, new Dictionary<string, string>());

            // Assert - Redacted entry should be skipped entirely
            results.Should().HaveCount(1);
            results[0].Entry.PlayerName.Should().Be("Normal");
        }

        [Fact]
        public void Calculate_ShouldHandleEmptySimulatedWinners()
        {
            // Arrange
            var games = new List<BowlGame>
            {
                new BowlGame { Id = "g1", TeamHome = "A", TeamAway = "B", TeamHomeScore = 20, TeamAwayScore = 10, Status = GameStatus.Final, PointValue = 10 }
            };

            var entry = new BracketEntry
            {
                Id = "e1",
                PlayerName = "Test",
                Picks = new Dictionary<string, string> { { "g1", "A" } }
            };

            // Act - No simulations at all
            var results = WhatIfScoringEngine.Calculate(games, new List<BracketEntry> { entry }, new Dictionary<string, string>());

            // Assert - Should use real results
            results[0].Score.Should().Be(10);
        }

        [Fact]
        public void Calculate_ShouldHandleInProgressGamesWithNoSimulation()
        {
            // Arrange
            var games = new List<BowlGame>
            {
                new BowlGame { Id = "g1", TeamHome = "A", TeamAway = "B", Status = GameStatus.InProgress, PointValue = 10 }
            };

            var entry = new BracketEntry
            {
                Id = "e1",
                PlayerName = "Test",
                Picks = new Dictionary<string, string> { { "g1", "A" } }
            };

            // Act
            var results = WhatIfScoringEngine.Calculate(games, new List<BracketEntry> { entry }, new Dictionary<string, string>());

            // Assert - Game undecided, pick alive, should count toward max
            results[0].Score.Should().Be(0);
            results[0].MaxPossible.Should().Be(10);
        }

        [Fact]
        public void Calculate_ShouldNotEliminateTBDTeams()
        {
            // Arrange - Game with TBD team (playoff placeholder)
            var games = new List<BowlGame>
            {
                new BowlGame { Id = "g1", TeamHome = "Winner R1", TeamAway = "TBD", TeamHomeScore = 20, TeamAwayScore = 10, Status = GameStatus.Final, PointValue = 10 }
            };

            var entry = new BracketEntry
            {
                Id = "e1",
                PlayerName = "Test",
                Picks = new Dictionary<string, string> { { "g1", "Winner R1" } }
            };

            // Act
            var results = WhatIfScoringEngine.Calculate(games, new List<BracketEntry> { entry }, new Dictionary<string, string>());

            // Assert
            results[0].Score.Should().Be(10);
        }

        #endregion
    }
}
