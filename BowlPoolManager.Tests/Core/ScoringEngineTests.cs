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
        public void Calculate_ShouldRankByScoreDesc_ThenByCorrectPicksDesc()
        {
            // Arrange
            // Game 1: Pts 10. Winner A.
            // Game 2: Pts 5. Winner C.
            var games = new List<BowlGame>
            {
                new BowlGame { Id = "1", TeamHome = "A", TeamAway = "B", TeamHomeScore = 20, TeamAwayScore = 10, Status = GameStatus.Final, PointValue = 10 },
                new BowlGame { Id = "2", TeamHome = "C", TeamAway = "D", TeamHomeScore = 20, TeamAwayScore = 10, Status = GameStatus.Final, PointValue = 5 }
            };

            // Entry 1: 15 Pts (Both Correct) - Rank 1
            // Entry 2: 10 Pts (Game 1 Correct)
            // Entry 3: 10 Pts (Game 1 Correct) - Tie with Entry 2
            // Entry 4: 10 Pts (Game 2 Correct? Impossible to get 10pts with just game 2) -> Let's say Entry 4 picked Game 1 Incorrectly, Game 2 Incorrectly? No.
            // Let's make a better scenario.
            
            // Re-Arrange
            // Game 1: 10 pts
            // Game 2: 10 pts
            games = new List<BowlGame>
            {
                new BowlGame { Id = "1", TeamHome = "A", TeamAwayScore = 0, TeamHomeScore = 10, Status = GameStatus.Final, PointValue = 10 },
                new BowlGame { Id = "2", TeamHome = "B", TeamAwayScore = 0, TeamHomeScore = 10, Status = GameStatus.Final, PointValue = 10 },
                new BowlGame { Id = "3", TeamHome = "C", TeamAwayScore = 0, TeamHomeScore = 10, Status = GameStatus.Final, PointValue = 5 }, // Tiebreaker extra game
            };

            var entries = new List<BracketEntry>
            {
                // User A: 20 Pts (2 Correct).
                new BracketEntry { Id = "A", PlayerName = "A", Picks = new Dictionary<string, string> { { "1", "A" }, { "2", "B" }, { "3", "X" } } },
                
                // User B: 20 Pts (3 Correct). (Got 1(10) + 2(10)? No, that's 20. + 3(5) = 25. 
                // Let's make User B: 10 + 5 + 5 = 20 pts? 
                
                // Simpler Scenario:
                // Game 1 (10pts), Game 2 (10pts).
                // User A: Correct 1, Correct 2. Total 20. Picks 2.
                // User B: Correct 1 Only. Total 10. Picks 1.
                // User C: Correct 2 Only. Total 10. Picks 1.
                
                // Needed: User D has SAME SCORE as User C, but MORE PICKS?
                // Game 3 (1pt).
                // User D: Incorrect 1(10), Incorrect 2(10). Correct 3(1). Total 1?
                // It's hard to have same score but different pick counts unless point values vary.
                
                // Scenario:
                // Game 1: 10 Pts.
                // Game 2: 5 Pts.
                // Game 3: 5 Pts.
                
                // User High: Get Game 1 (10pts). 1 Correct.
                // User Low: Get Game 2 (5) + Game 3 (5). 10 Pts. 2 Correct.
                
                // Expected: User Low > User High because 2 Correct > 1 Correct.
            };

             games = new List<BowlGame>
            {
                new BowlGame { Id = "1", TeamHome="W", TeamHomeScore=10, TeamAwayScore=0, Status=GameStatus.Final, PointValue=10 },
                new BowlGame { Id = "2", TeamHome="W", TeamHomeScore=10, TeamAwayScore=0, Status=GameStatus.Final, PointValue=5 },
                new BowlGame { Id = "3", TeamHome="W", TeamHomeScore=10, TeamAwayScore=0, Status=GameStatus.Final, PointValue=5 },
            };

            entries = new List<BracketEntry>
            {
                new BracketEntry { Id = "FewPicks", PlayerName = "FewPicks", Picks = new Dictionary<string, string> { { "1", "W" } } }, // 10 pts, 1 correct
                new BracketEntry { Id = "ManyPicks", PlayerName = "ManyPicks", Picks = new Dictionary<string, string> { { "2", "W" }, { "3", "W" } } }, // 10 pts, 2 correct
            };

            // Act
            var results = ScoringEngine.Calculate(games, entries);

            // Assert
            results.Should().HaveCount(2);
            results[0].Entry.PlayerName.Should().Be("ManyPicks"); // Rank 1
            results[0].Rank.Should().Be(1);

            results[1].Entry.PlayerName.Should().Be("FewPicks"); // Rank 2
            results[1].Rank.Should().Be(2); 
            // Note: If logic was just score, they'd be tied rank.
        }

        [Fact]
        public void Calculate_ShouldRankByTiebreaker_OnlyIfGameFinal()
        {
            // Arrange
            // Game T (Tiebreaker): Score 50.
            var games = new List<BowlGame>
            {
                new BowlGame { Id = "T", TeamHome="W", TeamHomeScore=25, TeamAwayScore=25, Status=GameStatus.Final, PointValue=10 },
            };

            // Two users, exact same score (10) and Picks (1).
            // User Close: Pred 40 (Delta 10).
            // User Far: Pred 70 (Delta 20).
            var entries = new List<BracketEntry>
            {
                new BracketEntry { Id = "Far", PlayerName = "Far", TieBreakerPoints=70, Picks = new Dictionary<string, string> { { "T", "W" } } }, // Delta 20
                new BracketEntry { Id = "Close", PlayerName = "Close", TieBreakerPoints=40, Picks = new Dictionary<string, string> { { "T", "W" } } }, // Delta 10
            };

            // Act
            var results = ScoringEngine.Calculate(games, entries, tieBreakerGameId: "T");

            // Assert
            results[0].Entry.PlayerName.Should().Be("Close");
            results[0].Rank.Should().Be(1);
            results[1].Entry.PlayerName.Should().Be("Far");
            results[1].Rank.Should().Be(2);
        }

        [Fact]
        public void Calculate_ShouldShareRank_IfTieBreakerNotFinal()
        {
            // Arrange
            // Game T (Tiebreaker): Not Final
            var games = new List<BowlGame>
            {
                new BowlGame { Id = "T", TeamHome="W", TeamHomeScore=0, TeamAwayScore=0, Status=GameStatus.InProgress, PointValue=10 },
            };

            var entries = new List<BracketEntry>
            {
                new BracketEntry { Id = "A", PlayerName = "A", TieBreakerPoints=10, Picks = new Dictionary<string, string>() },
                new BracketEntry { Id = "B", PlayerName = "B", TieBreakerPoints=100, Picks = new Dictionary<string, string>() },
            };

            // Act
            var results = ScoringEngine.Calculate(games, entries, tieBreakerGameId: "T");

            // Assert
            results[0].Rank.Should().Be(1);
            results[1].Rank.Should().Be(1); // Should break tie yet
        }
    }
}
