using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BowlPoolManager.Api.Functions;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Api.Services.ScoreFeed;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// Sign-in and role tests for the football write endpoints (pools, archive, games), against
    /// BUSINESS-RULES section 1: pools, archives and games are SuperAdmin. Admin results entry for
    /// games is undecided (#104), so the game tests do not assert the Admin tier.
    /// "Gets through" means the caller check passed and the repository or service behind it was called.
    /// </summary>
    public class EndpointAuthFootballPoolTests
    {
        private class Fixture
        {
            public Mock<IPoolRepository> Pools = new();
            public Mock<IUserRepository> Users = EndpointKit.Users();
            public Mock<IEntryRepository> Entries = new();
            public BowlPool Pool = new() { Id = "pool-1", SeasonId = "season-1", Name = "Family", InviteCode = "code", LockDate = DateTime.UtcNow.AddDays(7) };

            public Fixture()
            {
                Pools.Setup(p => p.GetPoolAsync(It.IsAny<string>())).ReturnsAsync(Pool);
                Entries.Setup(e => e.GetEntriesAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(new List<BracketEntry>());
            }

            public PoolFunctions Build() => new(new NullLoggerFactory(), Pools.Object, Users.Object, Entries.Object);
        }

        private static BowlPool NewPool() => new()
        {
            Id = "pool-1", SeasonId = "season-1", Name = "Family", InviteCode = "code", LockDate = DateTime.UtcNow.AddDays(7)
        };

        // ── CreatePool (POST) ─────────────────────────────────────────────────────

        [Fact]
        public async Task CreatePool_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();

            var result = await f.Build().CreatePool(EndpointKit.Request(Tier.Anonymous, body: NewPool()));

            EndpointKit.Status(result).Should().Be(401);
            f.Pools.Verify(p => p.AddPoolAsync(It.IsAny<BowlPool>()), Times.Never);
        }

        [Theory]
        [InlineData(Tier.Player)]
        [InlineData(Tier.Admin)]
        public async Task CreatePool_ShouldReturn403_WhenCallerIsNotSuperAdmin(Tier tier)
        {
            var f = new Fixture();

            var result = await f.Build().CreatePool(EndpointKit.Request(tier, body: NewPool()));

            EndpointKit.Status(result).Should().Be(403);
            f.Pools.Verify(p => p.AddPoolAsync(It.IsAny<BowlPool>()), Times.Never);
        }

        [Fact]
        public async Task CreatePool_ShouldSavePool_WhenSuperAdmin()
        {
            var f = new Fixture();

            var result = await f.Build().CreatePool(EndpointKit.Request(Tier.SuperAdmin, body: NewPool()));

            EndpointKit.Status(result).Should().Be(200);
            f.Pools.Verify(p => p.AddPoolAsync(It.IsAny<BowlPool>()), Times.Once);
        }

        // ── UpdatePool (PUT) ──────────────────────────────────────────────────────

        [Fact]
        public async Task UpdatePool_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();

            var result = await f.Build().UpdatePool(EndpointKit.Request(Tier.Anonymous, body: NewPool()));

            EndpointKit.Status(result).Should().Be(401);
            f.Pools.Verify(p => p.AddPoolAsync(It.IsAny<BowlPool>()), Times.Never);
        }

        [Theory]
        [InlineData(Tier.Player)]
        [InlineData(Tier.Admin)]
        public async Task UpdatePool_ShouldReturn403_WhenCallerIsNotSuperAdmin(Tier tier)
        {
            var f = new Fixture();

            var result = await f.Build().UpdatePool(EndpointKit.Request(tier, body: NewPool()));

            EndpointKit.Status(result).Should().Be(403);
            f.Pools.Verify(p => p.AddPoolAsync(It.IsAny<BowlPool>()), Times.Never);
        }

        [Fact]
        public async Task UpdatePool_ShouldSavePool_WhenSuperAdmin()
        {
            var f = new Fixture();

            var result = await f.Build().UpdatePool(EndpointKit.Request(Tier.SuperAdmin, body: NewPool()));

            EndpointKit.Status(result).Should().Be(200);
            f.Pools.Verify(p => p.AddPoolAsync(It.IsAny<BowlPool>()), Times.Once);
        }

        // ── DeletePool (DELETE) ───────────────────────────────────────────────────

        [Fact]
        public async Task DeletePool_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();

            var result = await f.Build().DeletePool(EndpointKit.Request(Tier.Anonymous), "pool-1");

            EndpointKit.Status(result).Should().Be(401);
            f.Pools.Verify(p => p.DeletePoolAsync(It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData(Tier.Player)]
        [InlineData(Tier.Admin)]
        public async Task DeletePool_ShouldReturn403_WhenCallerIsNotSuperAdmin(Tier tier)
        {
            var f = new Fixture();

            var result = await f.Build().DeletePool(EndpointKit.Request(tier), "pool-1");

            EndpointKit.Status(result).Should().Be(403);
            f.Pools.Verify(p => p.DeletePoolAsync(It.IsAny<string>()), Times.Never);
            f.Entries.Verify(e => e.DeleteEntryAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        // Business rules section 2 and issue 106: deleting a pool deletes its entries, each with its own partition key.
        [Fact]
        public async Task DeletePool_ShouldDeleteThePoolsEntries_WhenSuperAdmin()
        {
            var f = new Fixture();
            f.Entries.Setup(e => e.GetEntriesAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(new List<BracketEntry>
            {
                new() { Id = "e1", PoolId = "pool-1", SeasonId = "season-1" },
                new() { Id = "e2", PoolId = "pool-1", SeasonId = "season-1" }
            });

            var result = await f.Build().DeletePool(EndpointKit.Request(Tier.SuperAdmin), "pool-1");

            EndpointKit.Status(result).Should().Be(200);
            f.Entries.Verify(e => e.GetEntriesAsync("season-1", "pool-1"), Times.Once);
            f.Entries.Verify(e => e.DeleteEntryAsync("e1", "season-1"), Times.Once);
            f.Entries.Verify(e => e.DeleteEntryAsync("e2", "season-1"), Times.Once);
            f.Pools.Verify(p => p.DeletePoolAsync("pool-1"), Times.Once);
        }

        [Fact]
        public async Task DeletePool_ShouldKeepThePool_WhenDeletingAnEntryFails()
        {
            var f = new Fixture();
            f.Entries.Setup(e => e.GetEntriesAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(new List<BracketEntry>
            {
                new() { Id = "e1", PoolId = "pool-1", SeasonId = "season-1" }
            });
            f.Entries.Setup(e => e.DeleteEntryAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("boom"));

            var act = async () => await f.Build().DeletePool(EndpointKit.Request(Tier.SuperAdmin), "pool-1");

            await act.Should().ThrowAsync<InvalidOperationException>();
            f.Pools.Verify(p => p.DeletePoolAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DeletePool_ShouldReturn404AndDeleteNothing_WhenThePoolDoesNotExist()
        {
            var f = new Fixture();
            f.Pools.Setup(p => p.GetPoolAsync(It.IsAny<string>())).ReturnsAsync((BowlPool?)null);

            var result = await f.Build().DeletePool(EndpointKit.Request(Tier.SuperAdmin), "missing");

            EndpointKit.Status(result).Should().Be(404);
            f.Entries.Verify(e => e.DeleteEntryAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            f.Pools.Verify(p => p.DeletePoolAsync(It.IsAny<string>()), Times.Never);
        }

        // Business rules section 2: a new pool's lock date must be in the future. A new BowlPool always has a GUID Id,
        // so the check cannot depend on an empty Id.
        [Fact]
        public async Task CreatePool_ShouldRefuseAndSaveNothing_WhenTheLockDateIsInThePast()
        {
            var f = new Fixture();
            var pool = NewPool();
            pool.LockDate = DateTime.UtcNow.AddDays(-1);

            var result = await f.Build().CreatePool(EndpointKit.Request(Tier.SuperAdmin, body: pool));

            EndpointKit.Status(result).Should().Be(400);
            EndpointKit.BodyText(result).Should().Contain("Lock Date");
            f.Pools.Verify(p => p.AddPoolAsync(It.IsAny<BowlPool>()), Times.Never);
        }

        [Fact]
        public async Task CreatePool_ShouldRefuseAndSaveNothing_WhenTheInviteCodeIsMissing()
        {
            var f = new Fixture();
            var pool = NewPool();
            pool.InviteCode = " ";

            var result = await f.Build().CreatePool(EndpointKit.Request(Tier.SuperAdmin, body: pool));

            EndpointKit.Status(result).Should().Be(400);
            f.Pools.Verify(p => p.AddPoolAsync(It.IsAny<BowlPool>()), Times.Never);
        }

        [Fact]
        public async Task DeletePool_ShouldDeletePool_WhenSuperAdmin()
        {
            var f = new Fixture();

            var result = await f.Build().DeletePool(EndpointKit.Request(Tier.SuperAdmin), "pool-1");

            EndpointKit.Status(result).Should().Be(200);
            f.Pools.Verify(p => p.DeletePoolAsync("pool-1"), Times.Once);
        }

        // ── ToggleConclusion (POST) ───────────────────────────────────────────────

        [Fact]
        public async Task ToggleConclusion_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();

            var result = await f.Build().ToggleConclusion(EndpointKit.Request(Tier.Anonymous), "pool-1");

            EndpointKit.Status(result).Should().Be(401);
            f.Pools.Verify(p => p.AddPoolAsync(It.IsAny<BowlPool>()), Times.Never);
        }

        [Theory]
        [InlineData(Tier.Player)]
        [InlineData(Tier.Admin)]
        public async Task ToggleConclusion_ShouldReturn403_WhenCallerIsNotSuperAdmin(Tier tier)
        {
            var f = new Fixture();

            var result = await f.Build().ToggleConclusion(EndpointKit.Request(tier), "pool-1");

            EndpointKit.Status(result).Should().Be(403);
            f.Pools.Verify(p => p.AddPoolAsync(It.IsAny<BowlPool>()), Times.Never);
        }

        [Fact]
        public async Task ToggleConclusion_ShouldFlipConcludedFlag_WhenSuperAdmin()
        {
            var f = new Fixture();

            var result = await f.Build().ToggleConclusion(EndpointKit.Request(Tier.SuperAdmin), "pool-1");

            EndpointKit.Status(result).Should().Be(200);
            f.Pools.Verify(p => p.AddPoolAsync(It.Is<BowlPool>(x => x.IsConcluded)), Times.Once);
        }

        // ── ArchivePool (POST) ────────────────────────────────────────────────────

        private static ArchiveFunctions BuildArchive(Mock<IPoolRepository> pools, Mock<IArchiveRepository> archives, Mock<IUserRepository> users) =>
            new(new NullLoggerFactory(), pools.Object, new Mock<IGameRepository>().Object,
                new Mock<IEntryRepository>().Object, archives.Object, users.Object);

        [Fact]
        public async Task ArchivePool_ShouldReturn401_WhenAnonymous()
        {
            var pools = new Mock<IPoolRepository>();
            var archives = new Mock<IArchiveRepository>();

            var result = await BuildArchive(pools, archives, EndpointKit.Users()).ArchivePool(EndpointKit.Request(Tier.Anonymous), "pool-1");

            EndpointKit.Status(result).Should().Be(401);
            pools.Verify(p => p.GetPoolAsync(It.IsAny<string>()), Times.Never);
            archives.Verify(a => a.AddArchiveAsync(It.IsAny<PoolArchive>()), Times.Never);
        }

        [Theory]
        [InlineData(Tier.Player)]
        [InlineData(Tier.Admin)]
        public async Task ArchivePool_ShouldReturn403_WhenCallerIsNotSuperAdmin(Tier tier)
        {
            var pools = new Mock<IPoolRepository>();
            var archives = new Mock<IArchiveRepository>();

            var result = await BuildArchive(pools, archives, EndpointKit.Users()).ArchivePool(EndpointKit.Request(tier), "pool-1");

            EndpointKit.Status(result).Should().Be(403);
            pools.Verify(p => p.GetPoolAsync(It.IsAny<string>()), Times.Never);
            archives.Verify(a => a.AddArchiveAsync(It.IsAny<PoolArchive>()), Times.Never);
        }

        [Fact]
        public async Task ArchivePool_ShouldReachPoolLookup_WhenSuperAdmin()
        {
            var pools = new Mock<IPoolRepository>();
            pools.Setup(p => p.GetPoolAsync(It.IsAny<string>())).ReturnsAsync((BowlPool?)null);

            var result = await BuildArchive(pools, new Mock<IArchiveRepository>(), EndpointKit.Users()).ArchivePool(EndpointKit.Request(Tier.SuperAdmin), "pool-1");

            new[] { 401, 403 }.Should().NotContain(EndpointKit.Status(result));
            pools.Verify(p => p.GetPoolAsync(It.IsAny<string>()), Times.Once);
        }
    }

    public class EndpointAuthFootballGameTests
    {
        private class Fixture
        {
            public Mock<IGameRepository> Games = new();
            public Mock<IUserRepository> Users = EndpointKit.Users();
            public Mock<IScoreFeed> Feed = new();
            public Mock<ISeasonRepository> Seasons = new();
            public Mock<IGameScoringService> Scoring = new();

            public GameFunctions Build() => new(new NullLoggerFactory(), Games.Object, Users.Object, Feed.Object, Seasons.Object, Scoring.Object);
        }

        private static BowlGame NewGame() => new() { Id = "game-1", SeasonId = "season-1", BowlName = "Test Bowl" };

        // ── SaveGame (POST) ───────────────────────────────────────────────────────
        // Admin tier not asserted: Admin rights for results and linking are #104.

        [Fact]
        public async Task SaveGame_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();

            var result = await f.Build().SaveGame(EndpointKit.Request(Tier.Anonymous, body: NewGame()));

            EndpointKit.Status(result).Should().Be(401);
            f.Scoring.Verify(s => s.ProcessGameUpdateAsync(It.IsAny<BowlGame>()), Times.Never);
        }

        [Fact]
        public async Task SaveGame_ShouldReturn403_WhenCallerIsPlayer()
        {
            var f = new Fixture();

            var result = await f.Build().SaveGame(EndpointKit.Request(Tier.Player, body: NewGame()));

            EndpointKit.Status(result).Should().Be(403);
            f.Scoring.Verify(s => s.ProcessGameUpdateAsync(It.IsAny<BowlGame>()), Times.Never);
        }

        [Fact]
        public async Task SaveGame_ShouldProcessGame_WhenSuperAdmin()
        {
            var f = new Fixture();

            var result = await f.Build().SaveGame(EndpointKit.Request(Tier.SuperAdmin, body: NewGame()));

            EndpointKit.Status(result).Should().Be(200);
            f.Scoring.Verify(s => s.ProcessGameUpdateAsync(It.IsAny<BowlGame>()), Times.Once);
        }

        // ── DeleteGame (DELETE) ───────────────────────────────────────────────────
        // Admin tier not asserted: whether Admin may delete game definitions is undecided (#104).

        [Fact]
        public async Task DeleteGame_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();

            var result = await f.Build().DeleteGame(EndpointKit.Request(Tier.Anonymous, "?seasonId=season-1"), "game-1");

            EndpointKit.Status(result).Should().Be(401);
            f.Games.Verify(g => g.DeleteGameAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DeleteGame_ShouldReturn403_WhenCallerIsPlayer()
        {
            var f = new Fixture();

            var result = await f.Build().DeleteGame(EndpointKit.Request(Tier.Player, "?seasonId=season-1"), "game-1");

            EndpointKit.Status(result).Should().Be(403);
            f.Games.Verify(g => g.DeleteGameAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DeleteGame_ShouldDeleteGame_WhenSuperAdmin()
        {
            var f = new Fixture();

            var result = await f.Build().DeleteGame(EndpointKit.Request(Tier.SuperAdmin, "?seasonId=season-1"), "game-1");

            EndpointKit.Status(result).Should().Be(200);
            f.Games.Verify(g => g.DeleteGameAsync("game-1", "season-1"), Times.Once);
        }

        // ── ForcePropagation (POST) ───────────────────────────────────────────────
        // Admin tier not asserted: part of result entry (#104).

        [Fact]
        public async Task ForcePropagation_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();

            var result = await f.Build().ForcePropagation(EndpointKit.Request(Tier.Anonymous, "?seasonId=season-1"));

            EndpointKit.Status(result).Should().Be(401);
            f.Scoring.Verify(s => s.ForcePropagateAllAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ForcePropagation_ShouldReturn403_WhenCallerIsPlayer()
        {
            var f = new Fixture();

            var result = await f.Build().ForcePropagation(EndpointKit.Request(Tier.Player, "?seasonId=season-1"));

            EndpointKit.Status(result).Should().Be(403);
            f.Scoring.Verify(s => s.ForcePropagateAllAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ForcePropagation_ShouldPropagate_WhenSuperAdmin()
        {
            var f = new Fixture();

            var result = await f.Build().ForcePropagation(EndpointKit.Request(Tier.SuperAdmin, "?seasonId=season-1"));

            EndpointKit.Status(result).Should().Be(200);
            f.Scoring.Verify(s => s.ForcePropagateAllAsync("season-1"), Times.Once);
        }
    }
}
