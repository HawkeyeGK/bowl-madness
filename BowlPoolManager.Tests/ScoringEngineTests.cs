using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;
using Xunit;

namespace BowlPoolManager.Tests
{
    public class ScoringEngineTests
    {
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

            // Entry 3: 20 pts, 1 correct (Not possible with these points, but let's fake 1 correct worth 20)
            // Actually let's make g1 worth 20 and g2 worth 0 to test pick counts.
            // Let's adjust point values.
            game1.PointValue = 10;
            game2.PointValue = 10;

            // Revised Scenario:
            // Alice: Correct g1, g2 (20 pts, 2 correct). Delta 5.
            // Bob: Correct g1, g2 (20 pts, 2 correct). Delta 0.
            // Charlie: Correct g1 (10 pts). 

            var entries = new List<BracketEntry> { entry1, entry2 };
            var poolConfig = new BowlPool { TieBreakerGameId = "tb" }; // Default config

            // Act
            var leaderboard = ScoringEngine.Calculate(games, entries, poolConfig);

            // Assert
            // Both have 20 pts. Both have 2 correct. 
            // Bob has delta 0 (better), Alice has delta 5.
            // Expected: Bob #1, Alice #2.
            Assert.Equal("Bob", leaderboard[0].Entry.PlayerName);
            Assert.Equal("Alice", leaderboard[1].Entry.PlayerName);
        }

        [Fact]
        public void Calculate_ShouldPrioritizeDelta_WhenConfigured()
        {
            // Arrange
            // 3 Entries, all tied on Score (20).
            // Entry A: Correct Picks: 2, Delta: 10
            // Entry B: Correct Picks: 1, Delta: 5
            
            // To make Score tied but Picks differ, point values must be different.
            // Game 1: 20 pts. Game 2: 10 pts. Game 3: 10 pts.
            // Winner: H.
            // Entry A picks G2(H), G3(H). Score 20. Correct 2.
            // Entry B picks G1(H). Score 20. Correct 1.

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
            Assert.Equal("B_LowPicks_GoodDelta", leaderboard[0].Entry.PlayerName);
            Assert.Equal("A_HighPicks_BadDelta", leaderboard[1].Entry.PlayerName);
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
            Assert.Equal("A_HighPicks_BadDelta", leaderboard[0].Entry.PlayerName);
            Assert.Equal("B_LowPicks_GoodDelta", leaderboard[1].Entry.PlayerName);
        }
        
        [Fact]
        public void Calculate_ShouldAssignRanksCorrectly_WithTies()
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

             Assert.Equal(1, lb[0].Rank); // 1
             Assert.Equal(1, lb[1].Rank); // 2 is tied with 1
             Assert.Equal(3, lb[2].Rank); // 3 is last (Score 0)
        }
    }
}
