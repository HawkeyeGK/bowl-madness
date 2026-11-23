using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>
    /// The cases at the top (Characterization_) record what Path to Victory (WhatIf.razor, ComputeGameState and
    /// WhatIfScoringEngine) did before the simulator existed, so the new class can be checked against it.
    /// The rest cover issues 136, 109 and 127.
    /// </summary>
    public class BracketSimulatorTests
    {
        private static DateTime Day(int day, int hour = 18) => new(2026, 12, day, hour, 0, 0, DateTimeKind.Utc);

        private static BowlGame Standard(string id, string home, string away, int day, int points = 1, GameStatus status = GameStatus.Scheduled, int? hs = null, int? as_ = null) => new()
        {
            Id = id, BowlName = id, TeamHome = home, TeamAway = away, StartTime = Day(day), PointValue = points,
            Status = status, TeamHomeScore = hs, TeamAwayScore = as_
        };

        private static BracketEntry Entry(string name, params (string game, string team)[] picks) => new()
        {
            Id = name, PlayerName = name, Picks = picks.ToDictionary(p => p.game, p => p.team)
        };

        private static Dictionary<string, string> Choose(params (string game, string team)[] choices) =>
            choices.ToDictionary(c => c.game, c => c.team);

        // First round (Miami v Texas A&M) feeds a quarterfinal (Ohio State at home) which feeds a semifinal (seed at home).
        // The later slots hold "TBD", as they do before the real games are played.
        private static List<BowlGame> PlayoffThread(bool tbdText = true)
        {
            string tbd = tbdText ? "TBD" : "Winner of x";
            return new List<BowlGame>
            {
                new() { Id = "r1", BowlName = "R1", TeamHome = "Miami", TeamAway = "Texas A&M", StartTime = Day(20), PointValue = 1, IsPlayoff = true, Round = TournamentRound.Round1, NextGameId = "qf" },
                new() { Id = "qf", BowlName = "QF", TeamHome = "Ohio State", TeamAway = tbd, StartTime = Day(31), PointValue = 2, IsPlayoff = true, Round = TournamentRound.QuarterFinal, NextGameId = "sf" },
                new() { Id = "sf", BowlName = "SF", TeamHome = "Oregon", TeamAway = tbd, StartTime = Day(31, 23), PointValue = 4, IsPlayoff = true, Round = TournamentRound.SemiFinal }
            };
        }

        // ---------- Characterization of today's page ----------

        [Fact]
        public void Characterization_ShouldApplyAChosenWinnerOverARealResult()
        {
            var games = new List<BowlGame> { Standard("g1", "A", "B", 15, 10, GameStatus.Final, 20, 10) };

            var result = BracketSimulator.Run(games, new[] { Entry("p", ("g1", "B")) }, null, Choose(("g1", "B")));

            result.Rows[0].Score.Should().Be(10);
            result.Games["g1"].Winner.Should().Be("B");
            result.Games["g1"].WinnerIsReal.Should().BeFalse();
        }

        [Fact]
        public void Characterization_ShouldUseTheRealResult_WhenNothingIsChosen()
        {
            var games = new List<BowlGame> { Standard("g1", "A", "B", 15, 10, GameStatus.Final, 20, 10) };

            var result = BracketSimulator.Run(games, new[] { Entry("p", ("g1", "A")) }, null);

            result.Rows[0].Score.Should().Be(10);
            result.Games["g1"].WinnerIsReal.Should().BeTrue();
        }

        [Fact]
        public void Characterization_ShouldLeaveAnInProgressGameUndecided_WhenNothingIsChosen()
        {
            var games = new List<BowlGame> { Standard("g1", "A", "B", 15, 10, GameStatus.InProgress, 20, 10) };

            var result = BracketSimulator.Run(games, new[] { Entry("p", ("g1", "A")) }, null);

            result.Games["g1"].Winner.Should().BeNull();
            result.Rows[0].Score.Should().Be(0);
            result.Rows[0].MaxPossible.Should().Be(10);
        }

        [Fact]
        public void Characterization_ShouldOfferBothFeederTeams_WhenTheFeederIsUndecided()
        {
            var result = BracketSimulator.Run(PlayoffThread(), Array.Empty<BracketEntry>(), null);

            result.Games["qf"].Entrants.Should().BeEquivalentTo("Ohio State", "Miami", "Texas A&M");
            result.Games["qf"].Home.Should().Be("Ohio State");
            result.Games["qf"].Away.Should().BeNull();
        }

        [Fact]
        public void Characterization_ShouldFillTheNextGameWithTheFeedersWinner_WhenTheFeederIsChosen()
        {
            var result = BracketSimulator.Run(PlayoffThread(), Array.Empty<BracketEntry>(), null, Choose(("r1", "Miami")));

            result.Games["qf"].Away.Should().Be("Miami");
            result.Games["qf"].Entrants.Should().BeEquivalentTo("Ohio State", "Miami");
        }

        [Fact]
        public void Characterization_ShouldIgnoreAChoice_WhenTheTeamCannotReachTheGame()
        {
            var result = BracketSimulator.Run(PlayoffThread(), Array.Empty<BracketEntry>(), null,
                Choose(("r1", "Miami"), ("qf", "Texas A&M")));

            result.Games["qf"].Winner.Should().BeNull();
        }

        [Fact]
        public void Characterization_ShouldDropADownstreamChoice_WhenItsFeederChangesToAnotherTeam()
        {
            // QF was chosen as Miami, then R1 is changed to Texas A&M: Miami can no longer be in the QF.
            var result = BracketSimulator.Run(PlayoffThread(), Array.Empty<BracketEntry>(), null,
                Choose(("r1", "Texas A&M"), ("qf", "Miami")));

            result.Games["qf"].Winner.Should().BeNull();
        }

        [Fact]
        public void Characterization_ShouldIgnoreAStalePropagatedName_WhenTheFeederIsChosenAnotherWay()
        {
            // The server copied Miami into the QF, then the page chooses Texas A&M in R1.
            var games = PlayoffThread();
            games[1].TeamAway = "Miami";

            var result = BracketSimulator.Run(games, Array.Empty<BracketEntry>(), null, Choose(("r1", "Texas A&M")));

            result.Games["qf"].Away.Should().Be("Texas A&M");
            result.Games["qf"].Entrants.Should().NotContain("Miami");
        }

        [Fact]
        public void Characterization_ShouldSkipEntriesWithHiddenPicks()
        {
            var games = new List<BowlGame> { Standard("g1", "A", "B", 15) };
            var hidden = new BracketEntry { Id = "h", PlayerName = "Hidden", Picks = null };

            var result = BracketSimulator.Run(games, new[] { hidden, Entry("p", ("g1", "A")) }, null);

            result.Rows.Should().ContainSingle().Which.Entry.PlayerName.Should().Be("p");
        }

        [Fact]
        public void Characterization_ShouldMatchPicksIgnoringCase()
        {
            var games = new List<BowlGame> { Standard("g1", "Alabama", "B", 15, 10, GameStatus.Final, 20, 10) };

            var result = BracketSimulator.Run(games, new[] { Entry("p", ("g1", "alabama")) }, null);

            result.Rows[0].Score.Should().Be(10);
        }

        // ---------- #136 ranks ----------

        [Fact]
        public void Run_ShouldRankByTheLeaderboardOrder_WhenEntriesTieOnScoreButNotOnTiebreaker()
        {
            // Both score 3 and can still reach 5. Pool tiebreaker is correct-pick count, then name.
            var games = new List<BowlGame>
            {
                Standard("g1", "A", "B", 10, 3, GameStatus.Final, 20, 10),
                Standard("g2", "C", "D", 11, 1, GameStatus.Final, 20, 10),
                Standard("g3", "E", "F", 12, 1, GameStatus.Final, 20, 10),
                Standard("g4", "G", "H", 13, 5)
            };
            var threePointer = Entry("Zed", ("g1", "A"), ("g2", "D"), ("g3", "F"), ("g4", "G"));
            var threeCorrect = Entry("Amy", ("g1", "B"), ("g2", "C"), ("g3", "E"), ("g4", "G"));

            var result = BracketSimulator.Run(games, new[] { threeCorrect, threePointer }, new BowlPool());

            var leaderboard = ScoringEngine.Calculate(games, new List<BracketEntry> { threeCorrect, threePointer }, new BowlPool());
            result.Rows.Select(r => (r.Entry.PlayerName, r.Rank)).Should().Equal(leaderboard.Select(r => (r.Entry.PlayerName, r.Rank)));
            result.Rows.Select(r => r.Rank).Should().Equal(1, 2);
        }

        [Fact]
        public void Run_ShouldShareARank_WhenEntriesTieOnEverything()
        {
            var games = new List<BowlGame> { Standard("g1", "A", "B", 10, 1, GameStatus.Final, 20, 10) };

            var result = BracketSimulator.Run(games, new[] { Entry("Amy", ("g1", "A")), Entry("Bob", ("g1", "A")) }, new BowlPool());

            result.Rows.Select(r => r.Rank).Should().Equal(1, 1);
        }

        // ---------- #109 elimination ----------

        private static (List<BowlGame> games, List<BracketEntry> entries) FiveEntriesOneLeftBehind()
        {
            var games = new List<BowlGame>
            {
                Standard("g1", "A", "B", 10, 10, GameStatus.Final, 20, 10),
                Standard("g2", "C", "D", 11, 10, GameStatus.Final, 20, 10),
                Standard("g3", "E", "F", 12, 1)
            };
            var entries = new List<BracketEntry>
            {
                Entry("P1", ("g1", "A"), ("g2", "C"), ("g3", "E")), // 20
                Entry("P2", ("g1", "A"), ("g2", "C"), ("g3", "E")), // 20
                Entry("P3", ("g1", "A"), ("g2", "C"), ("g3", "E")), // 20
                Entry("P4", ("g1", "A"), ("g2", "C"), ("g3", "E")), // 20
                Entry("Behind", ("g1", "B"), ("g2", "D"), ("g3", "E")) // 0, max 1
            };
            return (games, entries);
        }

        [Fact]
        public void Run_ShouldMarkAnEntryOut_WhenItsMaxIsBelowThirdPlacesScore()
        {
            var (games, entries) = FiveEntriesOneLeftBehind();

            var result = BracketSimulator.Run(games, entries, null);

            result.Rows.Single(r => r.Entry.PlayerName == "Behind").CanFinishTop3.Should().BeFalse();
            result.Rows.Where(r => r.Entry.PlayerName != "Behind").Should().OnlyContain(r => r.CanFinishTop3);
        }

        [Fact]
        public void Run_ShouldNotMarkAnyoneOut_WhenThreeOrFewerEntries()
        {
            var (games, entries) = FiveEntriesOneLeftBehind();

            var result = BracketSimulator.Run(games, new[] { entries[0], entries[1], entries[4] }, null);

            result.Rows.Should().OnlyContain(r => r.CanFinishTop3);
        }

        [Fact]
        public void Run_ShouldKeepAnEntryIn_WhenItsMaxEqualsThirdPlacesScore()
        {
            var games = new List<BowlGame>
            {
                Standard("g1", "A", "B", 10, 10, GameStatus.Final, 20, 10),
                Standard("g2", "C", "D", 11, 10)
            };
            var entries = new List<BracketEntry>
            {
                Entry("P1", ("g1", "A")), Entry("P2", ("g1", "A")), Entry("P3", ("g1", "A")),
                Entry("Ties", ("g1", "B"), ("g2", "C")) // max 10 = third place's score: can still tie
            };

            var result = BracketSimulator.Run(games, entries, null);

            result.Rows.Single(r => r.Entry.PlayerName == "Ties").CanFinishTop3.Should().BeTrue();
        }

        [Fact]
        public void Run_ShouldMarkAnEntryOut_WhenAChosenResultTakesItBelowThirdPlace()
        {
            var games = new List<BowlGame> { Standard("g1", "A", "B", 10, 10), Standard("g2", "C", "D", 11, 1) };
            var entries = new List<BracketEntry>
            {
                Entry("P1", ("g1", "A")), Entry("P2", ("g1", "A")), Entry("P3", ("g1", "A")),
                Entry("Behind", ("g1", "B"))
            };

            var undecided = BracketSimulator.Run(games, entries, null);
            var chosen = BracketSimulator.Run(games, entries, null, Choose(("g1", "A")));

            undecided.Rows.Should().OnlyContain(r => r.CanFinishTop3);
            chosen.Rows.Single(r => r.Entry.PlayerName == "Behind").CanFinishTop3.Should().BeFalse();
        }

        // ---------- #127 simulated Max ----------

        [Fact]
        public void Run_ShouldNotCountALostTeamInMax_WhenItOnlyReachedTheGameInTheSimulation()
        {
            // R1: Miami wins (chosen). QF: Ohio State beats Miami (chosen). The QF away slot says "TBD" in the data.
            var picker = Entry("Picker", ("sf", "Miami")); // Miami to win the semifinal
            var other = Entry("Other", ("sf", "Oregon"));

            var before = BracketSimulator.Run(PlayoffThread(), new[] { picker, other }, null, Choose(("r1", "Miami")));
            var after = BracketSimulator.Run(PlayoffThread(), new[] { picker, other }, null, Choose(("r1", "Miami"), ("qf", "Ohio State")));

            before.Rows.Single(r => r.Entry.PlayerName == "Picker").MaxPossible.Should().Be(4);
            var pickerAfter = after.Rows.Single(r => r.Entry.PlayerName == "Picker");
            pickerAfter.MaxPossible.Should().Be(0);
            pickerAfter.Picks["sf"].Should().Be(PickState.Dead);
            after.Rows.Single(r => r.Entry.PlayerName == "Other").MaxPossible.Should().Be(4);
        }

        [Fact]
        public void Run_ShouldNotCountALostTeamInMax_WhenTheSlotTextIsAWinnerOfPlaceholder()
        {
            var picker = Entry("Picker", ("sf", "Miami"));

            var result = BracketSimulator.Run(PlayoffThread(tbdText: false), new[] { picker }, null,
                Choose(("r1", "Miami"), ("qf", "Ohio State")));

            result.Rows[0].MaxPossible.Should().Be(0);
        }

        [Fact]
        public void Run_ShouldEliminateTheSimulatedTeam_WhenARealResultIsKeptAfterAnUpstreamChange()
        {
            // Really: Miami won R1 and lost the QF to Ohio State. The page now chooses Texas A&M in R1, so Texas A&M
            // is the team that Ohio State's real win eliminates.
            var games = PlayoffThread();
            games[0].Status = GameStatus.Final; games[0].TeamHomeScore = 30; games[0].TeamAwayScore = 10; // Miami
            games[1].TeamAway = "Miami"; games[1].Status = GameStatus.Final; games[1].TeamHomeScore = 24; games[1].TeamAwayScore = 17;
            var picker = Entry("Picker", ("sf", "Texas A&M"));

            var result = BracketSimulator.Run(games, new[] { picker }, null, Choose(("r1", "Texas A&M")));

            result.Games["qf"].Winner.Should().Be("Ohio State");
            result.Rows[0].Picks["sf"].Should().Be(PickState.Dead);
            result.Rows[0].MaxPossible.Should().Be(0);
        }

        // ---------- pick states ----------

        [Fact]
        public void Run_ShouldReportEachPicksState()
        {
            var games = new List<BowlGame>
            {
                Standard("won", "A", "B", 10, 1, GameStatus.Final, 20, 10),
                Standard("lost", "C", "D", 11, 1, GameStatus.Final, 20, 10),
                Standard("dead", "A", "E", 12),
                Standard("open", "F", "G", 13)
            };
            var entry = Entry("p", ("won", "A"), ("lost", "D"), ("dead", "B"), ("open", "F"));
            // "dead": B already lost in "won", so a pick of B in a later game can no longer score.

            var result = BracketSimulator.Run(games, new[] { entry }, null);

            result.Rows[0].Picks.Should().Contain(new KeyValuePair<string, PickState>("won", PickState.Won))
                .And.Contain(new KeyValuePair<string, PickState>("lost", PickState.Lost))
                .And.Contain(new KeyValuePair<string, PickState>("dead", PickState.Dead))
                .And.Contain(new KeyValuePair<string, PickState>("open", PickState.Open));
        }

        // ---------- order warnings ----------

        [Fact]
        public void Run_ShouldWarn_WhenAPlayoffGameStartsBeforeAGameThatFeedsIt()
        {
            var games = PlayoffThread();
            games[1].StartTime = Day(10); // the QF now kicks off before R1

            var result = BracketSimulator.Run(games, Array.Empty<BracketEntry>(), null);

            result.Warnings.Should().ContainSingle().Which.Should().Match<SimulationWarning>(w => w.GameId == "qf" && w.FeederGameId == "r1");
        }

        [Fact]
        public void Run_ShouldNotWarn_WhenFeedersKickOffFirst()
        {
            BracketSimulator.Run(PlayoffThread(), Array.Empty<BracketEntry>(), null).Warnings.Should().BeEmpty();
        }

        [Fact]
        public void Run_ShouldNotLoopForever_WhenGamesFeedEachOther()
        {
            var a = Standard("a", "A", "B", 10); a.NextGameId = "b";
            var b = Standard("b", "TBD", "C", 11); b.NextGameId = "a";

            var act = () => BracketSimulator.Run(new List<BowlGame> { a, b }, Array.Empty<BracketEntry>(), null);

            act.Should().NotThrow();
        }
    }
}
