using Xunit;
using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    public class HoopsWhatIfScoringEngineTests
    {
        // ---------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------

        private static HoopsPool MakePool(Dictionary<TournamentRound, int>? pointsPerRound = null) =>
            new HoopsPool
            {
                Id = "pool1",
                PointsPerRound = pointsPerRound ?? new Dictionary<TournamentRound, int>
                {
                    { TournamentRound.RoundOf64,          1 },
                    { TournamentRound.RoundOf32,          2 },
                    { TournamentRound.Sweet16,             4 },
                    { TournamentRound.Elite8,              8 },
                    { TournamentRound.FinalFour,          16 },
                    { TournamentRound.NationalChampionship, 32 },
                }
            };

        private static HoopsGame MakeGame(string id, TournamentRound round,
            string home, string away, string? winner = null) =>
            new HoopsGame
            {
                Id = id,
                Round = round,
                TeamHome = home,
                TeamAway = away,
                Status = winner != null ? GameStatus.Final : GameStatus.Scheduled,
                TeamHomeScore = winner != null ? (winner == home ? 70 : 60) : null,
                TeamAwayScore = winner != null ? (winner == away ? 70 : 60) : null,
            };

        private static BracketEntry MakeEntry(string id, string playerName,
            Dictionary<string, string> picks) =>
            new BracketEntry { Id = id, PlayerName = playerName, Picks = picks };

        // ---------------------------------------------------------------------------
        // Empty input
        // ---------------------------------------------------------------------------

        [Fact]
        public void Calculate_ShouldReturnEmptyList_WhenEntriesListIsEmpty()
        {
            var games = new List<HoopsGame>
            {
                MakeGame("g1", TournamentRound.RoundOf64, "Duke", "UNC", "Duke")
            };

            var result = HoopsWhatIfScoringEngine.Calculate(
                games,
                new List<BracketEntry>(),
                MakePool(),
                new Dictionary<string, string?> { { "g1", "Duke" } });

            result.Should().BeEmpty();
        }

        // ---------------------------------------------------------------------------
        // All correct picks
        // ---------------------------------------------------------------------------

        [Fact]
        public void Calculate_ShouldScoreFullPoints_WhenAllPicksMatchSimulatedWinners()
        {
            var pool = MakePool(new Dictionary<TournamentRound, int>
            {
                { TournamentRound.RoundOf64, 1 },
                { TournamentRound.RoundOf32, 2 },
            });

            var games = new List<HoopsGame>
            {
                MakeGame("g1", TournamentRound.RoundOf64, "Duke", "UNC", "Duke"),
                MakeGame("g2", TournamentRound.RoundOf32, "Duke", "Kansas", "Duke"),
            };

            var entry = MakeEntry("e1", "Alice", new Dictionary<string, string>
            {
                { "g1", "Duke" },
                { "g2", "Duke" },
            });

            var simulated = new Dictionary<string, string?>
            {
                { "g1", "Duke" },
                { "g2", "Duke" },
            };

            var result = HoopsWhatIfScoringEngine.Calculate(games, new List<BracketEntry> { entry }, pool, simulated);

            result.Should().HaveCount(1);
            result[0].Score.Should().Be(3);       // 1 + 2
            result[0].MaxPossible.Should().Be(3);
            result[0].CorrectPicks.Should().Be(2);
        }

        // ---------------------------------------------------------------------------
        // All wrong picks for decided games
        // ---------------------------------------------------------------------------

        [Fact]
        public void Calculate_ShouldScoreZero_AndMaxPossibleZero_WhenAllPicksAreWrongForDecidedGames()
        {
            var pool = MakePool(new Dictionary<TournamentRound, int>
            {
                { TournamentRound.RoundOf64, 1 },
            });

            var games = new List<HoopsGame>
            {
                MakeGame("g1", TournamentRound.RoundOf64, "Duke", "UNC", "Duke"),
            };

            var entry = MakeEntry("e1", "Bob", new Dictionary<string, string>
            {
                { "g1", "UNC" }, // wrong
            });

            var simulated = new Dictionary<string, string?> { { "g1", "Duke" } };

            var result = HoopsWhatIfScoringEngine.Calculate(games, new List<BracketEntry> { entry }, pool, simulated);

            result[0].Score.Should().Be(0);
            result[0].MaxPossible.Should().Be(0);
            result[0].CorrectPicks.Should().Be(0);
        }

        // ---------------------------------------------------------------------------
        // Undecided game — alive pick contributes to MaxPossible
        // ---------------------------------------------------------------------------

        [Fact]
        public void Calculate_ShouldIncreaseMaxPossible_WhenPickIsAliveInUndecidedGame()
        {
            var pool = MakePool(new Dictionary<TournamentRound, int>
            {
                { TournamentRound.RoundOf64, 1 },
                { TournamentRound.RoundOf32, 2 },
            });

            // g1 decided: Duke beat UNC
            // g2 undecided: Duke vs Kansas (not yet played)
            var games = new List<HoopsGame>
            {
                MakeGame("g1", TournamentRound.RoundOf64, "Duke", "UNC", "Duke"),
                MakeGame("g2", TournamentRound.RoundOf32, "Duke", "Kansas"),
            };

            var entry = MakeEntry("e1", "Alice", new Dictionary<string, string>
            {
                { "g1", "Duke" },   // correct
                { "g2", "Duke" },   // alive — Duke is not eliminated
            });

            var simulated = new Dictionary<string, string?> { { "g1", "Duke" } };

            var result = HoopsWhatIfScoringEngine.Calculate(games, new List<BracketEntry> { entry }, pool, simulated);

            result[0].Score.Should().Be(1);           // g1 correct
            result[0].MaxPossible.Should().Be(1 + 2); // g1 + g2 potential
        }

        // ---------------------------------------------------------------------------
        // Undecided game — eliminated pick does NOT contribute to MaxPossible
        // ---------------------------------------------------------------------------

        [Fact]
        public void Calculate_ShouldNotIncreaseMaxPossible_WhenPickTeamIsEliminated()
        {
            var pool = MakePool(new Dictionary<TournamentRound, int>
            {
                { TournamentRound.RoundOf64, 1 },
                { TournamentRound.RoundOf32, 2 },
            });

            // g1 decided: Duke beat UNC — UNC is now eliminated
            // g2 undecided, but entry picked UNC (eliminated)
            var games = new List<HoopsGame>
            {
                MakeGame("g1", TournamentRound.RoundOf64, "Duke", "UNC", "Duke"),
                MakeGame("g2", TournamentRound.RoundOf32, "Duke", "Kansas"),
            };

            var entry = MakeEntry("e1", "Bob", new Dictionary<string, string>
            {
                { "g1", "UNC" },   // wrong
                { "g2", "UNC" },   // eliminated — should NOT add to MaxPossible
            });

            var simulated = new Dictionary<string, string?> { { "g1", "Duke" } };

            var result = HoopsWhatIfScoringEngine.Calculate(games, new List<BracketEntry> { entry }, pool, simulated);

            result[0].Score.Should().Be(0);
            result[0].MaxPossible.Should().Be(0); // UNC eliminated, g2 cannot be gained
        }

        // ---------------------------------------------------------------------------
        // PointsPerRound hydration: different rounds use their own point values
        // ---------------------------------------------------------------------------

        [Fact]
        public void Calculate_ShouldUseRoundSpecificPointValues_WhenScoringGames()
        {
            var pool = MakePool(new Dictionary<TournamentRound, int>
            {
                { TournamentRound.RoundOf64,   1  },
                { TournamentRound.Sweet16,      4  },
                { TournamentRound.FinalFour,    16 },
            });

            var games = new List<HoopsGame>
            {
                MakeGame("r64",  TournamentRound.RoundOf64, "A", "B", "A"),
                MakeGame("s16",  TournamentRound.Sweet16,   "A", "C", "A"),
                MakeGame("ff",   TournamentRound.FinalFour, "A", "D", "A"),
            };

            var entry = MakeEntry("e1", "Alice", new Dictionary<string, string>
            {
                { "r64", "A" },
                { "s16", "A" },
                { "ff",  "A" },
            });

            var simulated = new Dictionary<string, string?>
            {
                { "r64", "A" },
                { "s16", "A" },
                { "ff",  "A" },
            };

            var result = HoopsWhatIfScoringEngine.Calculate(games, new List<BracketEntry> { entry }, pool, simulated);

            result[0].Score.Should().Be(1 + 4 + 16); // 21
        }

        // ---------------------------------------------------------------------------
        // PointValue = 0 (round not in PointsPerRound) contributes nothing
        // ---------------------------------------------------------------------------

        [Fact]
        public void Calculate_ShouldContributeZero_WhenRoundHasNoPointValueInPool()
        {
            // Pool has NO entry for FirstFour
            var pool = MakePool(new Dictionary<TournamentRound, int>
            {
                { TournamentRound.RoundOf64, 1 },
            });

            var games = new List<HoopsGame>
            {
                MakeGame("ff1", TournamentRound.FirstFour, "X", "Y", "X"),
                MakeGame("r64", TournamentRound.RoundOf64, "X", "Z", "X"),
            };

            var entry = MakeEntry("e1", "Alice", new Dictionary<string, string>
            {
                { "ff1", "X" }, // correct but worth 0 — FirstFour not in pool config
                { "r64", "X" }, // correct and worth 1
            });

            var simulated = new Dictionary<string, string?>
            {
                { "ff1", "X" },
                { "r64", "X" },
            };

            var result = HoopsWhatIfScoringEngine.Calculate(games, new List<BracketEntry> { entry }, pool, simulated);

            result[0].Score.Should().Be(1);          // only r64 contributes
            result[0].CorrectPicks.Should().Be(1);   // ff1 skipped before correctPicks increment
        }

        // ---------------------------------------------------------------------------
        // Elimination detection: IsEliminated when MaxPossible < leaderScore
        // ---------------------------------------------------------------------------

        [Fact]
        public void Calculate_ShouldMarkEntryAsEliminated_WhenMaxPossibleIsLessThanLeaderScore()
        {
            var pool = MakePool(new Dictionary<TournamentRound, int>
            {
                { TournamentRound.RoundOf64, 10 },
                { TournamentRound.RoundOf32,  5 },
            });

            // g1 decided: Duke wins (worth 10)
            // g2 undecided (worth 5)
            var games = new List<HoopsGame>
            {
                MakeGame("g1", TournamentRound.RoundOf64, "Duke", "UNC",    "Duke"),
                MakeGame("g2", TournamentRound.RoundOf32, "Duke", "Kansas"),
            };

            // Leader: picked Duke for g1 (10 pts). Also picked Duke for g2 (alive → MaxPossible 15).
            var leader = MakeEntry("e1", "Leader", new Dictionary<string, string>
            {
                { "g1", "Duke" },
                { "g2", "Duke" },
            });

            // Trailer: wrong on g1, and picked eliminated UNC for g2.
            // Score=0, MaxPossible=0 → eliminated (0 < 10).
            var trailer = MakeEntry("e2", "Trailer", new Dictionary<string, string>
            {
                { "g1", "UNC" },
                { "g2", "UNC" },
            });

            var simulated = new Dictionary<string, string?> { { "g1", "Duke" } };

            var result = HoopsWhatIfScoringEngine.Calculate(
                games,
                new List<BracketEntry> { leader, trailer },
                pool,
                simulated);

            var leaderRow  = result.First(r => r.EntryId == "e1");
            var trailerRow = result.First(r => r.EntryId == "e2");

            leaderRow.IsEliminated.Should().BeFalse();
            trailerRow.IsEliminated.Should().BeTrue();
        }

        [Fact]
        public void Calculate_ShouldNotMarkLeaderAsEliminated_WhenLeaderHasBestScore()
        {
            var pool = MakePool(new Dictionary<TournamentRound, int>
            {
                { TournamentRound.RoundOf64, 10 },
            });

            var games = new List<HoopsGame>
            {
                MakeGame("g1", TournamentRound.RoundOf64, "Duke", "UNC", "Duke"),
            };

            var entry = MakeEntry("e1", "Leader", new Dictionary<string, string>
            {
                { "g1", "Duke" },
            });

            var simulated = new Dictionary<string, string?> { { "g1", "Duke" } };

            var result = HoopsWhatIfScoringEngine.Calculate(
                games, new List<BracketEntry> { entry }, pool, simulated);

            result[0].IsEliminated.Should().BeFalse();
        }

        // ---------------------------------------------------------------------------
        // Rank assignment: tied entries share the same rank (gap/competition ranking)
        // ---------------------------------------------------------------------------

        [Fact]
        public void Calculate_ShouldAssignSharedRank_ToTiedEntries()
        {
            var pool = MakePool(new Dictionary<TournamentRound, int>
            {
                { TournamentRound.RoundOf64, 10 },
            });

            var games = new List<HoopsGame>
            {
                MakeGame("g1", TournamentRound.RoundOf64, "Duke", "UNC", "Duke"),
            };

            // Alice and Bob both pick Duke (correct, tied at 10 pts)
            // Charlie picks UNC (wrong, 0 pts)
            var entries = new List<BracketEntry>
            {
                MakeEntry("e1", "Alice",   new Dictionary<string, string> { { "g1", "Duke" } }),
                MakeEntry("e2", "Bob",     new Dictionary<string, string> { { "g1", "Duke" } }),
                MakeEntry("e3", "Charlie", new Dictionary<string, string> { { "g1", "UNC"  } }),
            };

            var simulated = new Dictionary<string, string?> { { "g1", "Duke" } };

            var result = HoopsWhatIfScoringEngine.Calculate(games, entries, pool, simulated);

            var alice   = result.First(r => r.EntryId == "e1");
            var bob     = result.First(r => r.EntryId == "e2");
            var charlie = result.First(r => r.EntryId == "e3");

            alice.Rank.Should().Be(1);
            bob.Rank.Should().Be(1);
            charlie.Rank.Should().Be(3); // gap ranking: 1, 1, 3
        }

        // ---------------------------------------------------------------------------
        // Sorting: ties broken by MaxPossible desc, then PlayerName asc
        // ---------------------------------------------------------------------------

        [Fact]
        public void Calculate_ShouldSortTiedScoresByMaxPossibleDescending()
        {
            var pool = MakePool(new Dictionary<TournamentRound, int>
            {
                { TournamentRound.RoundOf64, 10 },
                { TournamentRound.RoundOf32,  5 },
            });

            // g1 decided: Duke wins (10 pts)
            // g2 undecided: Duke vs Kansas (5 pts potential)
            var games = new List<HoopsGame>
            {
                MakeGame("g1", TournamentRound.RoundOf64, "Duke", "UNC", "Duke"),
                MakeGame("g2", TournamentRound.RoundOf32, "Duke", "Kansas"),
            };

            // Both score 10 on g1. Alice also picked Duke for g2 (alive → MaxPossible 15).
            // Bob picked UNC for g2 (eliminated → MaxPossible 10).
            var entries = new List<BracketEntry>
            {
                MakeEntry("e2", "Bob",   new Dictionary<string, string> { { "g1", "Duke" }, { "g2", "UNC"  } }),
                MakeEntry("e1", "Alice", new Dictionary<string, string> { { "g1", "Duke" }, { "g2", "Duke" } }),
            };

            var simulated = new Dictionary<string, string?> { { "g1", "Duke" } };

            var result = HoopsWhatIfScoringEngine.Calculate(games, entries, pool, simulated);

            // Alice should appear first (higher MaxPossible at equal Score)
            result[0].EntryId.Should().Be("e1");
            result[1].EntryId.Should().Be("e2");
        }

        [Fact]
        public void Calculate_ShouldSortTiedScoresAndMaxPossibleByPlayerNameAscending()
        {
            var pool = MakePool(new Dictionary<TournamentRound, int>
            {
                { TournamentRound.RoundOf64, 10 },
            });

            var games = new List<HoopsGame>
            {
                MakeGame("g1", TournamentRound.RoundOf64, "Duke", "UNC", "Duke"),
            };

            // Both entries identical score and MaxPossible; alphabetical by PlayerName expected
            var entries = new List<BracketEntry>
            {
                MakeEntry("e2", "Zara",  new Dictionary<string, string> { { "g1", "Duke" } }),
                MakeEntry("e1", "Aaron", new Dictionary<string, string> { { "g1", "Duke" } }),
            };

            var simulated = new Dictionary<string, string?> { { "g1", "Duke" } };

            var result = HoopsWhatIfScoringEngine.Calculate(games, entries, pool, simulated);

            result[0].PlayerName.Should().Be("Aaron");
            result[1].PlayerName.Should().Be("Zara");
        }

        // ---------------------------------------------------------------------------
        // IsRealTeam — tested indirectly via loser-tracking behavior
        // ---------------------------------------------------------------------------

        [Fact]
        public void Calculate_ShouldNotTrackTBDAsEliminatedTeam()
        {
            // A game with TBD as one team slot: when TBD "loses", it must NOT be added
            // to the eliminated set, because picks against "TBD" would then be unfairly zeroed.
            var pool = MakePool(new Dictionary<TournamentRound, int>
            {
                { TournamentRound.RoundOf64, 1 },
                { TournamentRound.RoundOf32, 2 },
            });

            // g1: Duke beats TBD (TBD is the placeholder loser — should not be marked eliminated)
            // g2: undecided; entry picks "TBD" (though unusual, must not be blocked by elimination)
            var games = new List<HoopsGame>
            {
                new HoopsGame
                {
                    Id = "g1", Round = TournamentRound.RoundOf64,
                    TeamHome = "Duke", TeamAway = "TBD",
                    Status = GameStatus.Final,
                    TeamHomeScore = 80, TeamAwayScore = 60,
                },
                MakeGame("g2", TournamentRound.RoundOf32, "Duke", "Kansas"),
            };

            var entry = MakeEntry("e1", "Alice", new Dictionary<string, string>
            {
                { "g1", "Duke" },
                { "g2", "TBD" }, // pick is "TBD" — since TBD is not eliminated, alive pick
            });

            var simulated = new Dictionary<string, string?> { { "g1", "Duke" } };

            var result = HoopsWhatIfScoringEngine.Calculate(games, new List<BracketEntry> { entry }, pool, simulated);

            // g1: correct (1 pt), g2: TBD pick alive (MaxPossible +2)
            result[0].Score.Should().Be(1);
            result[0].MaxPossible.Should().Be(3); // 1 + 2
        }

        [Fact]
        public void Calculate_ShouldNotTrackWinnerOfPlaceholderAsEliminatedTeam()
        {
            // A game with "Winner of East R64" as one team slot: when it "loses",
            // that placeholder must NOT be added to eliminated teams.
            var pool = MakePool(new Dictionary<TournamentRound, int>
            {
                { TournamentRound.RoundOf32, 2 },
                { TournamentRound.Sweet16,   4 },
            });

            var placeholder = "Winner of East R64";

            var games = new List<HoopsGame>
            {
                new HoopsGame
                {
                    Id = "g1", Round = TournamentRound.RoundOf32,
                    TeamHome = "Duke", TeamAway = placeholder,
                    Status = GameStatus.Final,
                    TeamHomeScore = 70, TeamAwayScore = 60,
                },
                MakeGame("g2", TournamentRound.Sweet16, "Duke", "Kansas"),
            };

            var entry = MakeEntry("e1", "Alice", new Dictionary<string, string>
            {
                { "g1", placeholder }, // wrong
                { "g2", placeholder }, // pick is placeholder — not eliminated, so alive
            });

            var simulated = new Dictionary<string, string?> { { "g1", "Duke" } };

            var result = HoopsWhatIfScoringEngine.Calculate(games, new List<BracketEntry> { entry }, pool, simulated);

            // g1: wrong (0 pts). g2 undecided; pick is a placeholder (not eliminated) → MaxPossible += 4
            result[0].Score.Should().Be(0);
            result[0].MaxPossible.Should().Be(4);
        }

        // ---------------------------------------------------------------------------
        // PointsPerRound is null — should not throw, all games worth 0
        // ---------------------------------------------------------------------------

        [Fact]
        public void Calculate_ShouldReturnZeroScores_WhenPoolPointsPerRoundIsNull()
        {
            var pool = MakePool(null);
            pool.PointsPerRound = null;

            var games = new List<HoopsGame>
            {
                MakeGame("g1", TournamentRound.RoundOf64, "Duke", "UNC", "Duke"),
            };

            var entry = MakeEntry("e1", "Alice", new Dictionary<string, string>
            {
                { "g1", "Duke" },
            });

            var simulated = new Dictionary<string, string?> { { "g1", "Duke" } };

            var act = () => HoopsWhatIfScoringEngine.Calculate(
                games, new List<BracketEntry> { entry }, pool, simulated);

            act.Should().NotThrow();

            var result = HoopsWhatIfScoringEngine.Calculate(
                games, new List<BracketEntry> { entry }, pool, simulated);

            result[0].Score.Should().Be(0);
            result[0].MaxPossible.Should().Be(0);
        }

        // ---------------------------------------------------------------------------
        // Entry with null Picks is skipped entirely
        // ---------------------------------------------------------------------------

        [Fact]
        public void Calculate_ShouldSkipEntry_WhenPicksIsNull()
        {
            var pool = MakePool(new Dictionary<TournamentRound, int>
            {
                { TournamentRound.RoundOf64, 1 },
            });

            var games = new List<HoopsGame>
            {
                MakeGame("g1", TournamentRound.RoundOf64, "Duke", "UNC", "Duke"),
            };

            var entries = new List<BracketEntry>
            {
                MakeEntry("e1", "Normal", new Dictionary<string, string> { { "g1", "Duke" } }),
                new BracketEntry { Id = "e2", PlayerName = "NoPicks", Picks = null },
            };

            var simulated = new Dictionary<string, string?> { { "g1", "Duke" } };

            var result = HoopsWhatIfScoringEngine.Calculate(games, entries, pool, simulated);

            result.Should().HaveCount(1);
            result[0].EntryId.Should().Be("e1");
        }

        // ---------------------------------------------------------------------------
        // Case-insensitive pick matching
        // ---------------------------------------------------------------------------

        [Fact]
        public void Calculate_ShouldMatchPicks_CaseInsensitively()
        {
            var pool = MakePool(new Dictionary<TournamentRound, int>
            {
                { TournamentRound.RoundOf64, 5 },
            });

            var games = new List<HoopsGame>
            {
                MakeGame("g1", TournamentRound.RoundOf64, "Duke Blue Devils", "UNC", "Duke Blue Devils"),
            };

            var entry = MakeEntry("e1", "Alice", new Dictionary<string, string>
            {
                { "g1", "duke blue devils" }, // lowercase
            });

            var simulated = new Dictionary<string, string?> { { "g1", "Duke Blue Devils" } };

            var result = HoopsWhatIfScoringEngine.Calculate(games, new List<BracketEntry> { entry }, pool, simulated);

            result[0].Score.Should().Be(5);
        }

        // ---------------------------------------------------------------------------
        // simulatedWinners key not in games list is silently ignored
        // ---------------------------------------------------------------------------

        [Fact]
        public void Calculate_ShouldIgnoreSimulatedWinner_WhenGameIdNotInGamesList()
        {
            var pool = MakePool(new Dictionary<TournamentRound, int>
            {
                { TournamentRound.RoundOf64, 1 },
            });

            var games = new List<HoopsGame>
            {
                MakeGame("g1", TournamentRound.RoundOf64, "Duke", "UNC", "Duke"),
            };

            var entry = MakeEntry("e1", "Alice", new Dictionary<string, string>
            {
                { "g1", "Duke" },
            });

            // "ghost-id" is not in games — must not affect outcome
            var simulated = new Dictionary<string, string?>
            {
                { "g1",       "Duke"     },
                { "ghost-id", "SomeTeam" },
            };

            var result = HoopsWhatIfScoringEngine.Calculate(games, new List<BracketEntry> { entry }, pool, simulated);

            result.Should().HaveCount(1);
            result[0].Score.Should().Be(1);
        }

        // ---------------------------------------------------------------------------
        // HoopsWhatIfRow properties: EntryId and PlayerName are hydrated correctly
        // ---------------------------------------------------------------------------

        [Fact]
        public void Calculate_ShouldHydrateEntryIdAndPlayerName_OnResultRow()
        {
            var pool = MakePool(new Dictionary<TournamentRound, int>
            {
                { TournamentRound.RoundOf64, 1 },
            });

            var games = new List<HoopsGame>
            {
                MakeGame("g1", TournamentRound.RoundOf64, "Duke", "UNC", "Duke"),
            };

            var entry = MakeEntry("entry-abc", "Jane Smith", new Dictionary<string, string>
            {
                { "g1", "Duke" },
            });

            var simulated = new Dictionary<string, string?> { { "g1", "Duke" } };

            var result = HoopsWhatIfScoringEngine.Calculate(
                games, new List<BracketEntry> { entry }, pool, simulated);

            result[0].EntryId.Should().Be("entry-abc");
            result[0].PlayerName.Should().Be("Jane Smith");
        }

        // ---------------------------------------------------------------------------
        // Picks dictionary is copied to row as-is
        // ---------------------------------------------------------------------------

        [Fact]
        public void Calculate_ShouldCopyPicksDictionaryToRow()
        {
            var pool = MakePool(new Dictionary<TournamentRound, int>
            {
                { TournamentRound.RoundOf64, 1 },
            });

            var games = new List<HoopsGame>
            {
                MakeGame("g1", TournamentRound.RoundOf64, "Duke", "UNC", "Duke"),
            };

            var picks = new Dictionary<string, string> { { "g1", "Duke" } };
            var entry = MakeEntry("e1", "Alice", picks);
            var simulated = new Dictionary<string, string?> { { "g1", "Duke" } };

            var result = HoopsWhatIfScoringEngine.Calculate(
                games, new List<BracketEntry> { entry }, pool, simulated);

            result[0].Picks.Should().BeEquivalentTo(picks);
        }

        // ---------------------------------------------------------------------------
        // Multi-entry scenario: leaderScore used from sorted[0] (not arbitrary entry)
        // ---------------------------------------------------------------------------

        [Fact]
        public void Calculate_ShouldUseHighestScoreAsLeaderScore_ForEliminationCheck()
        {
            // Three entries: A=10, B=5, C=0.
            // C has MaxPossible=0 and should be eliminated (0 < 10).
            // B has MaxPossible=5 and should be eliminated (5 < 10).
            // A has MaxPossible=10 and is NOT eliminated.
            var pool = MakePool(new Dictionary<TournamentRound, int>
            {
                { TournamentRound.RoundOf64, 10 },
            });

            var games = new List<HoopsGame>
            {
                MakeGame("g1", TournamentRound.RoundOf64, "Duke", "UNC", "Duke"),
            };

            var entries = new List<BracketEntry>
            {
                MakeEntry("eA", "A", new Dictionary<string, string> { { "g1", "Duke" } }),
                MakeEntry("eB", "B", new Dictionary<string, string>()),            // no pick for g1
                MakeEntry("eC", "C", new Dictionary<string, string> { { "g1", "UNC" } }), // wrong
            };

            var simulated = new Dictionary<string, string?> { { "g1", "Duke" } };

            var result = HoopsWhatIfScoringEngine.Calculate(games, entries, pool, simulated);

            var rowA = result.First(r => r.EntryId == "eA");
            var rowB = result.First(r => r.EntryId == "eB");
            var rowC = result.First(r => r.EntryId == "eC");

            rowA.Score.Should().Be(10);
            rowA.IsEliminated.Should().BeFalse();

            rowB.Score.Should().Be(0);
            rowB.IsEliminated.Should().BeTrue(); // MaxPossible=0 < leaderScore=10

            rowC.Score.Should().Be(0);
            rowC.IsEliminated.Should().BeTrue(); // MaxPossible=0 < leaderScore=10
        }
    }
}
