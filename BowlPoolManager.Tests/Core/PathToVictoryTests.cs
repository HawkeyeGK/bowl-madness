using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>
    /// The "Who can still win, and what has to happen" summary on Path to Victory. It only reads the simulator's
    /// output (BracketSimulator), so these tests run the simulator and check the summary drawn from it.
    /// </summary>
    public class PathToVictoryTests
    {
        private static DateTime Day(int day, int hour = 18) => new(2026, 12, day, hour, 0, 0, DateTimeKind.Utc);

        private static BowlGame Game(string id, string home, string away, int day, int points, GameStatus status = GameStatus.Scheduled, int? hs = null, int? as_ = null) => new()
        {
            Id = id, BowlName = id + " Bowl", TeamHome = home, TeamAway = away, StartTime = Day(day), PointValue = points,
            Status = status, TeamHomeScore = hs, TeamAwayScore = as_
        };

        private static BracketEntry Entry(string name, params (string game, string team)[] picks) => new()
        {
            Id = name, PlayerName = name, Picks = picks.ToDictionary(p => p.game, p => p.team)
        };

        private static PathToVictoryView Summarize(List<BowlGame> games, params BracketEntry[] entries) =>
            PathToVictory.Summarize(BracketSimulator.Run(games, entries, null), games);

        // Adam leads 10-0 after the Final game; Sally and Steve can still catch him.
        private static List<BowlGame> Season() => new()
        {
            Game("Done", "A", "B", 15, 10, GameStatus.Final, 30, 20),
            Game("Sugar", "Georgia", "Texas", 20, 6),
            Game("Rose", "Oregon", "Ohio State", 21, 5),
            Game("Fiesta", "Miami", "Clemson", 22, 4)
        };

        [Fact]
        public void Summarize_ShouldListTheLeaderFirstAndSayHowFarBehindTheOthersAre_WhenScoresDiffer()
        {
            var view = Summarize(Season(),
                Entry("Adam", ("Done", "A"), ("Sugar", "Texas"), ("Rose", "Oregon"), ("Fiesta", "Miami")),
                Entry("Sally", ("Done", "B"), ("Sugar", "Georgia"), ("Rose", "Oregon"), ("Fiesta", "Clemson")));

            view.Contenders.Select(c => c.Row.Entry.PlayerName).Should().Equal("Adam", "Sally");
            view.Contenders[0].GapText.Should().Be("Leads Sally by 10");
            view.Contenders[1].GapText.Should().Be("10 behind Adam");
        }

        [Fact]
        public void Summarize_ShouldListOnlyOpenPicksThatDifferFromTheLeader_WhenEntryIsBehind()
        {
            var view = Summarize(Season(),
                Entry("Adam", ("Done", "A"), ("Sugar", "Texas"), ("Rose", "Oregon"), ("Fiesta", "Miami")),
                Entry("Sally", ("Done", "B"), ("Sugar", "Georgia"), ("Rose", "Oregon"), ("Fiesta", "Clemson")));

            var sally = view.Contenders[1];
            sally.Swings.Select(s => s.GameId).Should().Equal("Sugar", "Fiesta");
            sally.Swings[0].Text.Should().Be("Georgia over Texas (Sugar Bowl, 6 pts)");
            sally.Swings[1].Text.Should().Be("Clemson over Miami (Fiesta Bowl, 4 pts)");
        }

        [Fact]
        public void Summarize_ShouldCompareTheLeaderWithSecondPlace_WhenEntryIsFirst()
        {
            var view = Summarize(Season(),
                Entry("Adam", ("Done", "A"), ("Sugar", "Texas"), ("Rose", "Oregon"), ("Fiesta", "Miami")),
                Entry("Sally", ("Done", "B"), ("Sugar", "Georgia"), ("Rose", "Oregon"), ("Fiesta", "Clemson")));

            var adam = view.Contenders[0];
            adam.ComparedWith.Should().Be("Sally");
            adam.IsLeader.Should().BeTrue();
            adam.SwingsLabel.Should().Be("To stay ahead");
            view.Contenders[1].SwingsLabel.Should().Be("Needs");
            adam.Swings.Select(s => s.Text).Should().Equal(
                "Texas over Georgia (Sugar Bowl, 6 pts)",
                "Miami over Clemson (Fiesta Bowl, 4 pts)");
        }

        [Fact]
        public void Summarize_ShouldOrderSwingsByPointsThenKickoff_WhenSeveralDiffer()
        {
            var games = new List<BowlGame>
            {
                Game("Early", "A", "B", 10, 2),
                Game("Big", "C", "D", 20, 8),
                Game("Late", "E", "F", 25, 2)
            };

            var view = Summarize(games,
                Entry("Lead", ("Early", "A"), ("Big", "C"), ("Late", "E")),
                Entry("Chase", ("Early", "B"), ("Big", "D"), ("Late", "F")));

            view.Contenders[1].Swings.Select(s => s.GameId).Should().Equal("Big", "Early", "Late");
        }

        [Fact]
        public void Summarize_ShouldSayTeamWins_WhenTheLeadersPickInThatGameIsAlreadyDead()
        {
            // Playoff: the first round feeds the final. Adam's team (Texas A&M) lost the first round, so his final pick is dead.
            var games = new List<BowlGame>
            {
                new() { Id = "r1", BowlName = "First Round", TeamHome = "Miami", TeamAway = "Texas A&M", StartTime = Day(20), PointValue = 3, IsPlayoff = true,
                        Status = GameStatus.Final, TeamHomeScore = 30, TeamAwayScore = 10, NextGameId = "final" },
                new() { Id = "final", BowlName = "Final", TeamHome = "Ohio State", TeamAway = "TBD", StartTime = Day(30), PointValue = 10, IsPlayoff = true }
            };

            var view = Summarize(games,
                Entry("Adam", ("r1", "Miami"), ("final", "Texas A&M")),
                Entry("Sally", ("r1", "Texas A&M"), ("final", "Miami")));

            var sally = view.Contenders.Single(c => c.Row.Entry.PlayerName == "Sally");
            sally.Swings.Should().ContainSingle().Which.Text.Should().Be("Miami wins (Final, 10 pts)");
        }

        [Fact]
        public void Summarize_ShouldIncludeAnOpenPick_WhenTheLeaderHasNoPickForThatGame()
        {
            var games = new List<BowlGame> { Game("Done", "A", "B", 15, 5, GameStatus.Final, 30, 20), Game("Sugar", "Georgia", "Texas", 20, 6) };

            var view = Summarize(games, Entry("Adam", ("Done", "A")), Entry("Sally", ("Sugar", "Georgia")));

            view.Contenders[1].Swings.Should().ContainSingle().Which.Text.Should().Be("Georgia wins (Sugar Bowl, 6 pts)");
        }

        [Fact]
        public void Summarize_ShouldLeaveOutGamesWorthNoPoints_WhenPicksDiffer()
        {
            var games = new List<BowlGame> { Game("Done", "A", "B", 15, 5, GameStatus.Final, 30, 20), Game("Zero", "C", "D", 20, 0) };

            var view = Summarize(games, Entry("Adam", ("Done", "A"), ("Zero", "C")), Entry("Sally", ("Zero", "D")));

            view.Contenders[1].Swings.Should().BeEmpty();
        }

        [Fact]
        public void Summarize_ShouldSayLevel_WhenScoresAreEqual()
        {
            var view = Summarize(Season(),
                Entry("Adam", ("Sugar", "Texas")),
                Entry("Sally", ("Sugar", "Georgia")));

            view.Contenders.Should().OnlyContain(c => c.GapText.StartsWith("Level with"));
            view.Contenders[1].GapText.Should().Be($"Level with {view.Contenders[0].Row.Entry.PlayerName} on points");
        }

        [Fact]
        public void Summarize_ShouldPutEntriesThatCannotFinishTop3InOut_WhenThereAreMoreThanThree()
        {
            var games = new List<BowlGame> { Game("Done", "A", "B", 15, 10, GameStatus.Final, 30, 20), Game("Last", "C", "D", 20, 2) };

            var view = Summarize(games,
                Entry("One", ("Done", "A")), Entry("Two", ("Done", "A")), Entry("Three", ("Done", "A")),
                Entry("Four", ("Done", "B"), ("Last", "C")));

            view.Contenders.Select(c => c.Row.Entry.PlayerName).Should().BeEquivalentTo(new[] { "One", "Two", "Three" });
            view.Out.Should().ContainSingle().Which.Entry.PlayerName.Should().Be("Four");
        }

        [Fact]
        public void Summarize_ShouldMarkCanWin_WhenScorePlusDifferingGamesEqualsTheLeadersScore()
        {
            var games = new List<BowlGame>
            {
                Game("Done", "A", "B", 15, 10, GameStatus.Final, 30, 20),
                Game("Mid", "E", "F", 16, 6, GameStatus.Final, 30, 20),
                Game("Last", "C", "D", 20, 6)
            };

            // Leader has 16 and no pick in Last. Close has 10 and Last (6) can bring it to 16 exactly; Chaser has 6 and can reach 12.
            var view = Summarize(games,
                Entry("Leader", ("Done", "A"), ("Mid", "E")),
                Entry("Chaser", ("Mid", "E"), ("Last", "C")),
                Entry("Close", ("Done", "A"), ("Last", "C")));

            view.Contenders[0].Row.Entry.PlayerName.Should().Be("Leader");

            view.Contenders.Single(c => c.Row.Entry.PlayerName == "Leader").CanWin.Should().BeTrue();
            view.Contenders.Single(c => c.Row.Entry.PlayerName == "Close").CanWin.Should().BeTrue();
            view.Contenders.Single(c => c.Row.Entry.PlayerName == "Chaser").CanWin.Should().BeFalse();
        }

        [Fact]
        public void Summarize_ShouldNotMarkCanWin_WhenTheRemainingPointsAreOnPicksTheLeaderShares()
        {
            // Owner decision 2026-11-28: Adam 10, Sally 5, one game left worth 6 that both picked. Sally's max (11) passes
            // Adam's score, but every point she can still get, he gets too.
            var games = new List<BowlGame>
            {
                Game("Done", "A", "B", 15, 10, GameStatus.Final, 30, 20),
                Game("Small", "C", "D", 16, 5, GameStatus.Final, 30, 20),
                Game("Sugar", "Georgia", "Texas", 20, 6)
            };

            var view = Summarize(games,
                Entry("Adam", ("Done", "A"), ("Sugar", "Georgia")),
                Entry("Sally", ("Small", "C"), ("Sugar", "Georgia")));

            var sally = view.Contenders.Single(c => c.Row.Entry.PlayerName == "Sally");
            sally.Row.MaxPossible.Should().Be(11);
            sally.CanWin.Should().BeFalse();
        }

        [Fact]
        public void Summarize_ShouldMarkCanWin_WhenScorePlusDifferingGamesReachesTheLeader()
        {
            var games = new List<BowlGame>
            {
                Game("Done", "A", "B", 15, 10, GameStatus.Final, 30, 20),
                Game("Small", "C", "D", 16, 5, GameStatus.Final, 30, 20),
                Game("Sugar", "Georgia", "Texas", 20, 6)
            };

            var view = Summarize(games,
                Entry("Adam", ("Done", "A"), ("Sugar", "Texas")),
                Entry("Sally", ("Small", "C"), ("Sugar", "Georgia")));

            view.Contenders.Single(c => c.Row.Entry.PlayerName == "Sally").CanWin.Should().BeTrue();
        }

        [Fact]
        public void Summarize_ShouldFollowChosenWinners_WhenTheViewerHasChosenOne()
        {
            var games = Season();
            var entries = new[]
            {
                Entry("Adam", ("Done", "A"), ("Sugar", "Texas")),
                Entry("Sally", ("Done", "B"), ("Sugar", "Georgia"), ("Rose", "Oregon"))
            };

            var sim = BracketSimulator.Run(games, entries, null, new Dictionary<string, string> { ["Sugar"] = "Georgia" });
            var view = PathToVictory.Summarize(sim, games);

            var sally = view.Contenders.Single(c => c.Row.Entry.PlayerName == "Sally");
            sally.Swings.Select(s => s.GameId).Should().Equal("Rose");
            sally.GapText.Should().Be("4 behind Adam");
        }

        [Fact]
        public void Summarize_ShouldGiveNoSwingsAndSayWhy_WhenNoOpenGamesDiffer()
        {
            var games = new List<BowlGame> { Game("Done", "A", "B", 15, 10, GameStatus.Final, 30, 20) };

            var view = Summarize(games, Entry("Adam", ("Done", "A")), Entry("Sally", ("Done", "B")));

            view.Contenders[1].Swings.Should().BeEmpty();
            view.Contenders[1].NoSwingsText.Should().Be("No games left where their picks differ from Adam's.");
        }

        [Fact]
        public void Summarize_ShouldGiveOneLineWithNoComparison_WhenThePoolHasOneEntry()
        {
            var view = Summarize(Season(), Entry("Solo", ("Sugar", "Georgia")));

            view.Contenders.Should().ContainSingle();
            view.Contenders[0].ComparedWith.Should().BeNull();
            view.Contenders[0].GapText.Should().BeEmpty();
            view.Contenders[0].Swings.Should().BeEmpty();
            view.Contenders[0].NoSwingsText.Should().BeEmpty();
        }

        [Fact]
        public void Summarize_ShouldBeEmpty_WhenThereAreNoEntries()
        {
            var view = Summarize(Season());

            view.Contenders.Should().BeEmpty();
            view.Out.Should().BeEmpty();
        }

        [Fact]
        public void Summarize_ShouldWriteOnePoint_WhenAGameIsWorthOne()
        {
            var games = new List<BowlGame> { Game("Done", "A", "B", 15, 5, GameStatus.Final, 30, 20), Game("Small", "C", "D", 20, 1) };

            var view = Summarize(games, Entry("Adam", ("Done", "A"), ("Small", "C")), Entry("Sally", ("Small", "D")));

            view.Contenders[1].Swings.Single().Text.Should().Be("D over C (Small Bowl, 1 pt)");
        }

        // ---------- How many swing games a card shows ----------

        private static ContenderLine LineWithSwings(int count) => new()
        {
            Swings = Enumerable.Range(0, count).Select(i => new SwingGame($"g{i}", "Bowl", 1, "T", null, "T wins")).ToList()
        };

        [Fact]
        public void ShownByDefault_ShouldShowAll_WhenOnlyOneMoreWouldBeHidden()
        {
            var line = LineWithSwings(4);

            line.ShownByDefault.Should().Be(4);
            line.HiddenByDefault.Should().Be(0);
        }

        [Fact]
        public void ShownByDefault_ShouldShowThreeAndHideTheRest_WhenThereAreFiveOrMore()
        {
            var line = LineWithSwings(5);

            line.ShownByDefault.Should().Be(3);
            line.HiddenByDefault.Should().Be(2);
        }

        [Fact]
        public void ShownByDefault_ShouldShowAll_WhenThereAreThreeOrFewer()
        {
            LineWithSwings(2).ShownByDefault.Should().Be(2);
            LineWithSwings(0).HiddenByDefault.Should().Be(0);
        }

        // ---------- Which pools the page shows (pick visibility) ----------

        [Fact]
        public void ShowsPool_ShouldBeFalse_WhenThePoolHasNotLocked()
        {
            var pool = new BowlPool { LockDate = Day(10) };

            PathToVictory.ShowsPool(pool, Day(9)).Should().BeFalse();
        }

        [Fact]
        public void ShowsPool_ShouldBeTrue_WhenThePoolHasLocked()
        {
            var pool = new BowlPool { LockDate = Day(10) };

            PathToVictory.ShowsPool(pool, Day(10).AddTicks(1)).Should().BeTrue();
            PathToVictory.ShowsPool(pool, Day(11)).Should().BeTrue();
        }

        [Fact]
        public void ShowsPool_ShouldBeFalse_WhenItIsExactlyTheLockTime()
        {
            // Entries can still change up to and including the lock date (server rule), so picks stay hidden then.
            var pool = new BowlPool { LockDate = Day(10) };

            PathToVictory.ShowsPool(pool, Day(10)).Should().BeFalse();
        }

        [Fact]
        public void ShowsPool_ShouldBeFalse_WhenThePoolIsArchived()
        {
            var pool = new BowlPool { LockDate = Day(10), IsArchived = true };

            PathToVictory.ShowsPool(pool, Day(11)).Should().BeFalse();
        }
    }
}
