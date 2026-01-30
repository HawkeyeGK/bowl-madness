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
            var results = ScoringEngine.Calculate(games, entries, new BowlPool { TieBreakerGameId = "T" });

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
            var results = ScoringEngine.Calculate(games, entries, new BowlPool { TieBreakerGameId = "T" });

            // Assert
            results[0].Rank.Should().Be(1);
            results[1].Rank.Should().Be(1); // Should break tie yet
        }

        #region Edge Case Tests

        [Fact]
        public void Calculate_ShouldHandleEmptyGamesList()
        {
            // Arrange
            var games = new List<BowlGame>();
            var entries = new List<BracketEntry>
            {
                new BracketEntry { Id = "e1", PlayerName = "Test", Picks = new Dictionary<string, string>() }
            };

            // Act
            var results = ScoringEngine.Calculate(games, entries);

            // Assert
            results.Should().HaveCount(1);
            results[0].Score.Should().Be(0);
            results[0].MaxPossible.Should().Be(0);
            results[0].CorrectPicks.Should().Be(0);
        }

        [Fact]
        public void Calculate_ShouldHandleEmptyEntriesList()
        {
            // Arrange
            var games = new List<BowlGame>
            {
                new BowlGame { Id = "g1", TeamHome = "A", TeamAway = "B", Status = GameStatus.Final, PointValue = 10 }
            };
            var entries = new List<BracketEntry>();

            // Act
            var results = ScoringEngine.Calculate(games, entries);

            // Assert
            results.Should().BeEmpty();
        }

        [Fact]
        public void Calculate_ShouldHandleEntryWithNoPicksForAnyGame()
        {
            // Arrange
            var games = new List<BowlGame>
            {
                new BowlGame { Id = "g1", TeamHome = "A", TeamAway = "B", TeamHomeScore = 10, TeamAwayScore = 5, Status = GameStatus.Final, PointValue = 10 }
            };
            var entry = new BracketEntry
            {
                Id = "e1",
                PlayerName = "Empty Picker",
                Picks = new Dictionary<string, string>() // No picks at all
            };

            // Act
            var results = ScoringEngine.Calculate(games, new List<BracketEntry> { entry });

            // Assert
            results.Should().HaveCount(1);
            results[0].Score.Should().Be(0);
            results[0].CorrectPicks.Should().Be(0);
        }

        [Fact]
        public void Calculate_ShouldCalculateRoundScoresCorrectly()
        {
            // Arrange
            var games = new List<BowlGame>
            {
                new BowlGame { Id = "s1", TeamHome = "A", TeamAway = "B", TeamHomeScore = 10, TeamAwayScore = 5, Status = GameStatus.Final, PointValue = 5, Round = PlayoffRound.Standard },
                new BowlGame { Id = "q1", TeamHome = "C", TeamAway = "D", TeamHomeScore = 10, TeamAwayScore = 5, Status = GameStatus.Final, PointValue = 10, Round = PlayoffRound.QuarterFinal },
                new BowlGame { Id = "sf1", TeamHome = "E", TeamAway = "F", TeamHomeScore = 10, TeamAwayScore = 5, Status = GameStatus.Final, PointValue = 20, Round = PlayoffRound.SemiFinal }
            };

            var entry = new BracketEntry
            {
                Id = "e1",
                PlayerName = "Test",
                Picks = new Dictionary<string, string>
                {
                    { "s1", "A" },  // Standard - Correct
                    { "q1", "C" },  // QuarterFinal - Correct
                    { "sf1", "E" }  // SemiFinal - Correct
                }
            };

            // Act
            var results = ScoringEngine.Calculate(games, new List<BracketEntry> { entry });

            // Assert
            results[0].Score.Should().Be(35); // 5 + 10 + 20
            results[0].RoundScores[PlayoffRound.Standard].Should().Be(5);
            results[0].RoundScores[PlayoffRound.QuarterFinal].Should().Be(10);
            results[0].RoundScores[PlayoffRound.SemiFinal].Should().Be(20);
            results[0].RoundScores[PlayoffRound.Championship].Should().Be(0);
        }

        [Fact]
        public void Calculate_ShouldHandleCaseInsensitiveTeamMatching()
        {
            // Arrange
            var games = new List<BowlGame>
            {
                new BowlGame { Id = "g1", TeamHome = "Ohio State", TeamAway = "Michigan", TeamHomeScore = 42, TeamAwayScore = 27, Status = GameStatus.Final, PointValue = 10 }
            };

            var entry = new BracketEntry
            {
                Id = "e1",
                PlayerName = "Test",
                Picks = new Dictionary<string, string>
                {
                    { "g1", "OHIO STATE" } // Uppercase version
                }
            };

            // Act
            var results = ScoringEngine.Calculate(games, new List<BracketEntry> { entry });

            // Assert
            results[0].Score.Should().Be(10); // Should match despite case difference
            results[0].CorrectPicks.Should().Be(1);
        }

        [Fact]
        public void Calculate_ShouldHandleNullPicksDictionary_ForRedactedEntries()
        {
            // Arrange
            var games = new List<BowlGame>
            {
                new BowlGame { Id = "g1", TeamHome = "A", TeamAway = "B", TeamHomeScore = 10, TeamAwayScore = 5, Status = GameStatus.Final, PointValue = 10 }
            };

            var redactedEntry = new BracketEntry
            {
                Id = "e1",
                PlayerName = "Redacted User",
                Picks = null // Picks are redacted/hidden
            };

            // Act
            var results = ScoringEngine.Calculate(games, new List<BracketEntry> { redactedEntry });

            // Assert
            results.Should().HaveCount(1);
            results[0].Score.Should().Be(0);
            results[0].MaxPossible.Should().Be(0);
            results[0].RoundScores.Should().NotBeNull();
            results[0].RoundScores[PlayoffRound.Standard].Should().Be(0);
        }

        #endregion

        #region Configurable Tiebreaker Tests

        [Fact]
        public void Calculate_ShouldSortByScore_ThenPicks_ThenDelta_ByDefault()
        {
            // Arrange
            var game1 = new BowlGame { Id = "g1", TeamHome = "H", TeamAway = "A", TeamHomeScore = 10, TeamAwayScore = 5, Status = GameStatus.Final, PointValue = 10, Round = PlayoffRound.Standard };
            var game2 = new BowlGame { Id = "g2", TeamHome = "H2", TeamAway = "A2", TeamHomeScore = 10, TeamAwayScore = 5, Status = GameStatus.Final, PointValue = 10, Round = PlayoffRound.Standard };
            
            // Tiebreaker Game (Total Points = 15)
            var tbGame = new BowlGame { Id = "tb", TeamHome = "TBH", TeamAway = "TBA", TeamHomeScore = 10, TeamAwayScore = 5, Status = GameStatus.Final };
            
            var games = new List<BowlGame> { game1, game2, tbGame };

            // Entry 1: 20 pts, 2 correct, Delta 5 (Predicted 20, Actual 15)
            var entry1 = new BracketEntry 
            { 
                Id = "e1", PlayerName = "Alice", 
                Picks = new Dictionary<string, string> { { "g1", "H" }, { "g2", "H2" } },
                TieBreakerPoints = 20
            };

            // Entry 2: 20 pts, 2 correct, Delta 0 (Predicted 15, Actual 15)
            var entry2 = new BracketEntry 
            { 
                Id = "e2", PlayerName = "Bob", 
                Picks = new Dictionary<string, string> { { "g1", "H" }, { "g2", "H2" } },
                TieBreakerPoints = 15
            };

            var entries = new List<BracketEntry> { entry1, entry2 };
            var poolConfig = new BowlPool { TieBreakerGameId = "tb" }; // Default config

            // Act
            var leaderboard = ScoringEngine.Calculate(games, entries, poolConfig);

            // Assert
            // Both have 20 pts. Both have 2 correct. 
            // Bob has delta 0 (better), Alice has delta 5.
            // Expected: Bob #1, Alice #2.
            leaderboard[0].Entry.PlayerName.Should().Be("Bob");
            leaderboard[1].Entry.PlayerName.Should().Be("Alice");
        }

        [Fact]
        public void Calculate_ShouldPrioritizeDelta_WhenConfigured()
        {
            // Arrange
            var g1 = new BowlGame { Id = "g1", TeamHome = "H", TeamAway = "A", TeamHomeScore = 1, TeamAwayScore = 0, Status = GameStatus.Final, PointValue = 20, Round = PlayoffRound.Standard };
            var g2 = new BowlGame { Id = "g2", TeamHome = "H", TeamAway = "A", TeamHomeScore = 1, TeamAwayScore = 0, Status = GameStatus.Final, PointValue = 10, Round = PlayoffRound.Standard };
            var g3 = new BowlGame { Id = "g3", TeamHome = "H", TeamAway = "A", TeamHomeScore = 1, TeamAwayScore = 0, Status = GameStatus.Final, PointValue = 10, Round = PlayoffRound.Standard };

            var tb = new BowlGame { Id = "tb", TeamHomeScore = 10, TeamAwayScore = 10, Status = GameStatus.Final }; // Total 20

            var games = new List<BowlGame> { g1, g2, g3, tb };

            var entryA = new BracketEntry 
            { 
                PlayerName = "A_HighPicks_BadDelta",
                Picks = new Dictionary<string, string> { { "g2", "H" }, { "g3", "H" } }, // 20pts, 2 picks
                TieBreakerPoints = 30 // Delta 10
            };

            var entryB = new BracketEntry 
            { 
                PlayerName = "B_LowPicks_GoodDelta",
                Picks = new Dictionary<string, string> { { "g1", "H" } }, // 20pts, 1 pick
                TieBreakerPoints = 25 // Delta 5
            };

            var entries = new List<BracketEntry> { entryA, entryB };

            // CONFIG: Primary = ScoreDelta, Secondary = CorrectPickCount
            var poolConfig = new BowlPool 
            { 
                TieBreakerGameId = "tb",
                PrimaryTieBreaker = TieBreakerMetric.ScoreDelta,
                SecondaryTieBreaker = TieBreakerMetric.CorrectPickCount
            };

            // Act
            var leaderboard = ScoringEngine.Calculate(games, entries, poolConfig);

            // Assert
            // Ties on Score (20).
            // Primary is Delta. B (5) < A (10). B wins.
            leaderboard[0].Entry.PlayerName.Should().Be("B_LowPicks_GoodDelta");
            leaderboard[1].Entry.PlayerName.Should().Be("A_HighPicks_BadDelta");
        }

        [Fact]
        public void Calculate_ShouldPrioritizePicks_WhenConfigured_Inverted()
        {
             // Same setup as above, but with default config (Picks first)
             
            var g1 = new BowlGame { Id = "g1", TeamHome = "H", TeamAway = "A", TeamHomeScore = 1, TeamAwayScore = 0, Status = GameStatus.Final, PointValue = 20, Round = PlayoffRound.Standard };
            var g2 = new BowlGame { Id = "g2", TeamHome = "H", TeamAway = "A", TeamHomeScore = 1, TeamAwayScore = 0, Status = GameStatus.Final, PointValue = 10, Round = PlayoffRound.Standard };
            var g3 = new BowlGame { Id = "g3", TeamHome = "H", TeamAway = "A", TeamHomeScore = 1, TeamAwayScore = 0, Status = GameStatus.Final, PointValue = 10, Round = PlayoffRound.Standard };

            var tb = new BowlGame { Id = "tb", TeamHomeScore = 10, TeamAwayScore = 10, Status = GameStatus.Final }; // Total 20
            var games = new List<BowlGame> { g1, g2, g3, tb };

            var entryA = new BracketEntry 
            { 
                PlayerName = "A_HighPicks_BadDelta",
                Picks = new Dictionary<string, string> { { "g2", "H" }, { "g3", "H" } }, // 20pts, 2 picks
                TieBreakerPoints = 30 // Delta 10
            };

            var entryB = new BracketEntry 
            { 
                PlayerName = "B_LowPicks_GoodDelta",
                Picks = new Dictionary<string, string> { { "g1", "H" } }, // 20pts, 1 pick
                TieBreakerPoints = 25 // Delta 5
            };

            var entries = new List<BracketEntry> { entryA, entryB };
            
            // Default Config (Picks -> Delta)
            var poolConfig = new BowlPool { TieBreakerGameId = "tb" }; 

            // Act
            var leaderboard = ScoringEngine.Calculate(games, entries, poolConfig);

            // Assert
            // Ties on Score. Picks: A (2) > B (1). A wins.
            leaderboard[0].Entry.PlayerName.Should().Be("A_HighPicks_BadDelta");
            leaderboard[1].Entry.PlayerName.Should().Be("B_LowPicks_GoodDelta");
        }
        
        [Fact]
        public void Calculate_ShouldAssignRanksCorrectly_WithTiesOnAllMetrics()
        {
             var g1 = new BowlGame { Id = "g1", TeamHome = "H", TeamAway = "A", TeamHomeScore = 10, TeamAwayScore = 5, Status = GameStatus.Final, PointValue = 10, Round = PlayoffRound.Standard };
             var tb = new BowlGame { Id = "tb", TeamHomeScore = 10, TeamAwayScore = 10, Status = GameStatus.Final }; // Total 20
             var games = new List<BowlGame> { g1, tb };

             var e1 = new BracketEntry { PlayerName = "1", Picks = new Dictionary<string,string>{{"g1","H"}}, TieBreakerPoints=20 }; // Sc:10, P:1, D:0
             var e2 = new BracketEntry { PlayerName = "2", Picks = new Dictionary<string,string>{{"g1","H"}}, TieBreakerPoints=20 }; // Sc:10, P:1, D:0
             var e3 = new BracketEntry { PlayerName = "3", Picks = new Dictionary<string,string>{{"g1","A"}}, TieBreakerPoints=20 }; // Sc:0,  P:0, D:0

             var entries = new List<BracketEntry> { e1, e2, e3 };
             var poolConfig = new BowlPool { TieBreakerGameId = "tb"};

             var lb = ScoringEngine.Calculate(games, entries, poolConfig);

             lb[0].Rank.Should().Be(1); // 1
             lb[1].Rank.Should().Be(1); // 2 is tied with 1
             lb[2].Rank.Should().Be(3); // 3 is last (Score 0)
        }

        #endregion
    }
}
