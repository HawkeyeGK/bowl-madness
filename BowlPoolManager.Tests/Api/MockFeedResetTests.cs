using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Api.Services.ScoreFeed;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// Switching the mock off can undo every result it produced: scores, status, detail, and the teams that
    /// propagation moved into later games. A slot is only cleared when it holds the feeder's winner.
    /// </summary>
    public class MockFeedResetTests
    {
        private static BowlGame G(string id, string name, string home = "H", string away = "A", bool linked = true,
            GameStatus status = GameStatus.Final, int? hs = 10, int? aws = 3, string? next = null) => new()
        {
            Id = id, SeasonId = "season-1", BowlName = name,
            ExternalId = linked ? "ext-" + id : null,
            TeamHome = home, TeamAway = away, Status = status, TeamHomeScore = hs, TeamAwayScore = aws,
            GameDetail = status == GameStatus.Final ? "Final" : null, NextGameId = next
        };

        // ── What is reset ────────────────────────────────────────────────────

        [Fact]
        public void Build_ShouldSelectLinkedGamesWithAResult_WhenGamesAreMixed()
        {
            var games = new List<BowlGame>
            {
                G("final", "Linked Final"),
                G("live", "Linked Live", status: GameStatus.InProgress, hs: 7, aws: 0),
                G("quiet", "Linked Scheduled", status: GameStatus.Scheduled, hs: null, aws: null),
                G("hand", "Hand Entered", linked: false)
            };

            var plan = MockFeedReset.Build(games);

            plan.GameIds.Should().BeEquivalentTo(new[] { "final", "live" });
        }

        [Fact]
        public void Build_ShouldListTheNamesOfTheGamesToReset_WhenSummarized()
        {
            var games = new List<BowlGame> { G("g1", "Bowl One"), G("g2", "Bowl Two"), G("hand", "Hand", linked: false) };

            var summary = MockFeedReset.Build(games).ToSummary();

            summary.GamesToReset.Should().Be(2);
            summary.GameNames.Should().Equal("Bowl One", "Bowl Two");
        }

        [Fact]
        public void Apply_ShouldClearScoresStatusAndDetail_WhenGameIsReset()
        {
            var games = new List<BowlGame> { G("g1", "Bowl One"), G("hand", "Hand", linked: false) };

            var changed = MockFeedReset.Apply(MockFeedReset.Build(games), games);

            changed.Select(g => g.Id).Should().Equal("g1");
            games[0].TeamHomeScore.Should().BeNull();
            games[0].TeamAwayScore.Should().BeNull();
            games[0].Status.Should().Be(GameStatus.Scheduled);
            games[0].GameDetail.Should().BeNull();
            games[1].Status.Should().Be(GameStatus.Final);
            games[1].TeamHomeScore.Should().Be(10);
        }

        [Fact]
        public void Build_ShouldNotClearAnySlot_WhenFinalHasNoWinner()
        {
            var feeder = G("q", "Quarter", hs: null, aws: null, next: "s");
            var semi = G("s", "Semi", home: "H", away: "Winner of Quarter", linked: false, status: GameStatus.Scheduled, hs: null, aws: null);

            var plan = MockFeedReset.Build(new List<BowlGame> { feeder, semi });

            plan.GameIds.Should().Equal("q");
            plan.SlotClears.Should().BeEmpty();
        }

        // ── Propagated teams ─────────────────────────────────────────────────

        [Fact]
        public void Apply_ShouldRestoreOnlyTheSlotHoldingTheWinner_WhenAnotherSlotHoldsASeededTeam()
        {
            var quarter = G("q", "Quarter One", home: "Alpha", away: "Bravo", hs: 20, aws: 10, next: "s");
            var semi = G("s", "Semi One", home: "Seed One", away: "Alpha", linked: false, status: GameStatus.Scheduled, hs: null, aws: null);
            semi.AwayTeamInfo = new TeamInfo { School = "Alpha" };
            semi.TeamAwaySeed = 5;
            semi.HomeTeamInfo = new TeamInfo { School = "Seed One" };
            semi.TeamHomeSeed = 1;
            var games = new List<BowlGame> { quarter, semi };

            var changed = MockFeedReset.Apply(MockFeedReset.Build(games), games);

            semi.TeamHome.Should().Be("Seed One");
            semi.HomeTeamInfo.Should().NotBeNull();
            semi.TeamHomeSeed.Should().Be(1);
            semi.TeamAway.Should().Be("Winner of Quarter One");
            semi.AwayTeamInfo.Should().BeNull();
            semi.TeamAwaySeed.Should().BeNull();
            changed.Select(g => g.Id).Should().BeEquivalentTo(new[] { "q", "s" });
        }

        [Fact]
        public void Apply_ShouldRestoreTheHomeSlot_WhenTheWinnerSitsAtHome()
        {
            var quarter = G("q", "Quarter One", home: "Alpha", away: "Bravo", next: "s");
            var semi = G("s", "Semi One", home: "ALPHA", away: "Seed Two", linked: false, status: GameStatus.Scheduled, hs: null, aws: null);
            var games = new List<BowlGame> { quarter, semi };

            MockFeedReset.Apply(MockFeedReset.Build(games), games);

            semi.TeamHome.Should().Be("Winner of Quarter One");
            semi.TeamAway.Should().Be("Seed Two");
        }

        [Fact]
        public void Apply_ShouldLeaveTheSlotsAndListIt_WhenNeitherSlotHoldsTheWinner()
        {
            var quarter = G("q", "Quarter One", home: "Alpha", away: "Bravo", next: "s");
            var semi = G("s", "Semi One", home: "Typed By Hand", away: "Also By Hand", linked: false, status: GameStatus.Scheduled, hs: null, aws: null);
            var games = new List<BowlGame> { quarter, semi };
            var plan = MockFeedReset.Build(games);

            var changed = MockFeedReset.Apply(plan, games);

            semi.TeamHome.Should().Be("Typed By Hand");
            semi.TeamAway.Should().Be("Also By Hand");
            plan.LeftAlone.Should().ContainSingle().Which.Should().Contain("Semi One");
            changed.Select(g => g.Id).Should().Equal("q");
        }

        [Fact]
        public void Apply_ShouldLeaveTheSlotsAndListIt_WhenBothSlotsHoldTheWinner()
        {
            var quarter = G("q", "Quarter One", home: "Alpha", away: "Bravo", next: "s");
            var semi = G("s", "Semi One", home: "Alpha", away: "Alpha", linked: false, status: GameStatus.Scheduled, hs: null, aws: null);
            var games = new List<BowlGame> { quarter, semi };
            var plan = MockFeedReset.Build(games);

            MockFeedReset.Apply(plan, games);

            semi.TeamHome.Should().Be("Alpha");
            semi.TeamAway.Should().Be("Alpha");
            plan.LeftAlone.Should().HaveCount(1);
        }

        [Fact]
        public void Apply_ShouldUndoTwoRounds_WhenAWinnerMovedThroughTwoLinkedGames()
        {
            var quarter = G("q", "Quarter One", home: "Alpha", away: "Bravo", hs: 21, aws: 7, next: "s");
            var semi = G("s", "Semi One", home: "Alpha", away: "Charlie", hs: 30, aws: 10, next: "f");
            var final = G("f", "Final One", home: "Alpha", away: "Delta", linked: false, status: GameStatus.Scheduled, hs: null, aws: null);
            var games = new List<BowlGame> { quarter, semi, final };

            var plan = MockFeedReset.Build(games);
            MockFeedReset.Apply(plan, games);

            semi.TeamHome.Should().Be("Winner of Quarter One");
            semi.TeamAway.Should().Be("Charlie");
            semi.Status.Should().Be(GameStatus.Scheduled);
            semi.TeamHomeScore.Should().BeNull();
            final.TeamHome.Should().Be("Winner of Semi One");
            final.TeamAway.Should().Be("Delta");
        }

        [Fact]
        public void Apply_ShouldNotTouchAHandEnteredResult_WhenALaterGameIsNotLinked()
        {
            var quarter = G("q", "Quarter One", home: "Alpha", away: "Bravo", next: "s");
            var semi = G("s", "Semi One", home: "Alpha", away: "Charlie", linked: false, hs: 14, aws: 3);
            var games = new List<BowlGame> { quarter, semi };
            var plan = MockFeedReset.Build(games);

            var changed = MockFeedReset.Apply(plan, games);

            semi.TeamHome.Should().Be("Alpha");
            semi.Status.Should().Be(GameStatus.Final);
            semi.TeamHomeScore.Should().Be(14);
            plan.NeedsReview.Should().ContainSingle().Which.Should().Contain("Semi One");
            changed.Select(g => g.Id).Should().Equal("q");
        }

        [Fact]
        public void Build_ShouldNotChangeAnyGame_WhenOnlyPlanning()
        {
            var quarter = G("q", "Quarter One", home: "Alpha", away: "Bravo", next: "s");
            var semi = G("s", "Semi One", home: "Alpha", away: "Seed", linked: false, status: GameStatus.Scheduled, hs: null, aws: null);

            MockFeedReset.Build(new List<BowlGame> { quarter, semi });

            quarter.Status.Should().Be(GameStatus.Final);
            quarter.TeamHomeScore.Should().Be(10);
            semi.TeamHome.Should().Be("Alpha");
        }

        // ── Service: partition key, confirmation, chunking ───────────────────

        private class ServiceFixture
        {
            public Mock<IGameRepository> Games = new();
            public Mock<ISeasonRepository> Seasons = new();
            public List<(List<BowlGame> Games, string SeasonId)> Saved = new();

            public ServiceFixture(List<BowlGame> games, bool hasCurrentSeason = true)
            {
                Seasons.Setup(s => s.GetSeasonsAsync()).ReturnsAsync(hasCurrentSeason
                    ? new List<Season> { new() { Id = "season-1", SeasonId = "2026", IsCurrent = true }, new() { Id = "old", SeasonId = "2025" } }
                    : new List<Season> { new() { Id = "old", SeasonId = "2025" } });
                Games.Setup(g => g.GetGamesAsync(It.IsAny<string?>())).ReturnsAsync(games);
                Games.Setup(g => g.UpdateGamesAsBatchAsync(It.IsAny<List<BowlGame>>(), It.IsAny<string>()))
                    .Callback((List<BowlGame> list, string season) => Saved.Add((list.ToList(), season)))
                    .Returns(Task.CompletedTask);
            }

            public MockFeedResetService Build() => new(Games.Object, Seasons.Object, NullLogger<MockFeedResetService>.Instance);
        }

        [Fact]
        public async Task PreviewAsync_ShouldReadTheCurrentSeasonByIdAndSaveNothing_WhenGamesHaveResults()
        {
            var f = new ServiceFixture(new List<BowlGame> { G("g1", "One"), G("g2", "Two") });

            var result = await f.Build().PreviewAsync();

            result.Status.Should().Be(MockFeedResetStatus.Ok);
            result.Summary!.GamesToReset.Should().Be(2);
            f.Games.Verify(g => g.GetGamesAsync("season-1"), Times.Once);
            f.Games.Verify(g => g.UpdateGamesAsBatchAsync(It.IsAny<List<BowlGame>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ApplyAsync_ShouldSaveWithTheSeasonPartitionKey_WhenCountMatches()
        {
            var f = new ServiceFixture(new List<BowlGame> { G("g1", "One"), G("g2", "Two") });

            var result = await f.Build().ApplyAsync(2);

            result.Status.Should().Be(MockFeedResetStatus.Ok);
            f.Saved.Should().ContainSingle();
            f.Saved[0].SeasonId.Should().Be("season-1");
            f.Saved[0].Games.Select(g => g.Id).Should().BeEquivalentTo(new[] { "g1", "g2" });
            f.Saved[0].Games.Should().OnlyContain(g => g.Status == GameStatus.Scheduled && g.TeamHomeScore == null);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(0)]
        public async Task ApplyAsync_ShouldRefuseAndSaveNothing_WhenCountDiffersFromThePreview(int confirm)
        {
            var f = new ServiceFixture(new List<BowlGame> { G("g1", "One"), G("g2", "Two") });

            var result = await f.Build().ApplyAsync(confirm);

            result.Status.Should().Be(MockFeedResetStatus.CountChanged);
            f.Saved.Should().BeEmpty();
        }

        [Fact]
        public async Task Service_ShouldReturnNoSeasonAndTouchNoGames_WhenNoSeasonIsCurrent()
        {
            var f = new ServiceFixture(new List<BowlGame> { G("g1", "One") }, hasCurrentSeason: false);

            (await f.Build().PreviewAsync()).Status.Should().Be(MockFeedResetStatus.NoSeason);
            (await f.Build().ApplyAsync(1)).Status.Should().Be(MockFeedResetStatus.NoSeason);

            f.Games.Verify(g => g.GetGamesAsync(It.IsAny<string?>()), Times.Never);
            f.Saved.Should().BeEmpty();
        }

        [Fact]
        public async Task ApplyAsync_ShouldSaveInBatchesOfAtMost100_WhenManyGamesChange()
        {
            var many = Enumerable.Range(0, 250).Select(i => G("g" + i, "Game " + i)).ToList();
            var f = new ServiceFixture(many);

            await f.Build().ApplyAsync(250);

            f.Saved.Should().HaveCount(3);
            f.Saved.Should().OnlyContain(b => b.Games.Count <= 100 && b.SeasonId == "season-1");
            f.Saved.Sum(b => b.Games.Count).Should().Be(250);
        }
    }
}
