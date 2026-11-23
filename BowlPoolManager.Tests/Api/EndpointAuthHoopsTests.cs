using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BowlPoolManager.Api.Functions;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// Sign-in, owner and role tests for the hoops entry endpoints. Same rules as football
    /// (BUSINESS-RULES sections 1 and 3: what an Admin can do is the same in both sports).
    /// Tests that name an issue cover a fixed hole.
    /// </summary>
    public class EndpointAuthHoopsEntryTests
    {
        private const string SecretPick = "SecretPickU";

        private class Fixture
        {
            public Mock<IHoopsEntryRepository> Entries = new();
            public Mock<IHoopsPoolRepository> Pools = new();
            public Mock<IHoopsGameRepository> Games = new();
            public Mock<IUserRepository> Users = EndpointKit.Users();
            public BracketEntry? Saved;

            public Fixture()
            {
                Pools.Setup(p => p.GetPoolAsync("pool-open")).ReturnsAsync(MakePool("pool-open", locked: false));
                Pools.Setup(p => p.GetPoolAsync("pool-locked")).ReturnsAsync(MakePool("pool-locked", locked: true));
                Games.Setup(g => g.GetGamesAsync(It.IsAny<string>())).ReturnsAsync(new List<HoopsGame> { new() { Id = "game-1" } });
                Entries.Setup(e => e.IsBracketNameTakenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(false);
                Entries.Setup(e => e.AddEntryAsync(It.IsAny<BracketEntry>()))
                    .Callback<BracketEntry>(e => Saved = e)
                    .Returns(Task.CompletedTask);
            }

            public void Stored(BracketEntry entry) =>
                Entries.Setup(e => e.GetEntryAsync(entry.Id, entry.SeasonId)).ReturnsAsync(entry);

            public HoopsEntryFunctions Build() => new(new NullLoggerFactory(), Entries.Object, Pools.Object, Games.Object, Users.Object);

            public void NothingSaved() => Entries.Verify(e => e.AddEntryAsync(It.IsAny<BracketEntry>()), Times.Never);
            public void NothingDeleted() => Entries.Verify(e => e.DeleteEntryAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        private static HoopsPool MakePool(string id, bool locked) => new()
        {
            Id = id,
            SeasonId = "season-1",
            Name = id,
            InviteCode = "code",
            LockDate = locked ? DateTime.UtcNow.AddDays(-1) : DateTime.UtcNow.AddDays(7)
        };

        private static BracketEntry MakeEntry(string id, string userId, string poolId = "pool-open", string pick = "Team A") => new()
        {
            Id = id,
            SeasonId = "season-1",
            PoolId = poolId,
            UserId = userId,
            PlayerName = "Bracket " + id,
            Picks = new Dictionary<string, string> { ["game-1"] = pick }
        };

        // ── CreateHoopsEntry (POST) ───────────────────────────────────────────────

        [Fact]
        public async Task CreateHoopsEntry_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();

            var result = await f.Build().CreateHoopsEntry(EndpointKit.Request(Tier.Anonymous, body: MakeEntry("e1", "")));

            EndpointKit.Status(result).Should().Be(401);
            f.NothingSaved();
        }

        [Fact]
        public async Task CreateHoopsEntry_ShouldRecordEntryAsCallers_WhenPlayerSavesBeforeLock()
        {
            var f = new Fixture();
            var body = MakeEntry("e1", EndpointKit.OtherPlayerId); // a player cannot save an entry as someone else

            var result = await f.Build().CreateHoopsEntry(EndpointKit.Request(Tier.Player, body: body));

            EndpointKit.Status(result).Should().Be(200);
            f.Saved.Should().NotBeNull();
            f.Saved!.UserId.Should().Be(EndpointKit.PlayerId);
        }

        [Fact]
        public async Task CreateHoopsEntry_ShouldReturn403_WhenPlayerEditsAnotherPlayersEntry()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.OtherPlayerId));

            var result = await f.Build().CreateHoopsEntry(EndpointKit.Request(Tier.Player, body: MakeEntry("e1", EndpointKit.OtherPlayerId, pick: "Team B")));

            EndpointKit.Status(result).Should().Be(403);
            f.NothingSaved();
        }

        [Fact]
        public async Task CreateHoopsEntry_ShouldRefuseNewEntry_WhenPlayerJoinsLockedPool()
        {
            var f = new Fixture();

            var result = await f.Build().CreateHoopsEntry(EndpointKit.Request(Tier.Player, body: MakeEntry("e1", "", "pool-locked")));

            EndpointKit.Status(result).Should().Be(400);
            f.NothingSaved();
        }

        [Fact]
        public async Task CreateHoopsEntry_ShouldRefusePickChange_WhenPlayerEditsOwnEntryInLockedPool()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId, "pool-locked", "Team A"));

            var result = await f.Build().CreateHoopsEntry(EndpointKit.Request(Tier.Player, body: MakeEntry("e1", EndpointKit.PlayerId, "pool-locked", "Team B")));

            EndpointKit.Status(result).Should().Be(400);
            f.NothingSaved();
        }

        [Theory]
        [InlineData(Tier.Admin)]
        [InlineData(Tier.SuperAdmin)]
        public async Task CreateHoopsEntry_ShouldSaveAnotherPlayersEntry_WhenCallerIsAdminOrAbove(Tier tier)
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId));

            var result = await f.Build().CreateHoopsEntry(EndpointKit.Request(tier, body: MakeEntry("e1", EndpointKit.PlayerId, pick: "Team B")));

            EndpointKit.Status(result).Should().Be(200);
            f.Saved.Should().NotBeNull();
            f.Saved!.UserId.Should().Be(EndpointKit.PlayerId, "an Admin edit keeps the owner unless assignToMe is set");
        }

        [Theory]
        [InlineData(Tier.Admin)]
        [InlineData(Tier.SuperAdmin)]
        public async Task CreateHoopsEntry_ShouldIgnoreLock_WhenCallerIsAdminOrAbove(Tier tier)
        {
            var f = new Fixture();

            var result = await f.Build().CreateHoopsEntry(EndpointKit.Request(tier, body: MakeEntry("e1", EndpointKit.PlayerId, "pool-locked")));

            EndpointKit.Status(result).Should().Be(200);
            f.Saved.Should().NotBeNull();
        }

        // #99. A player's save must never set payment status.
        [Fact]
        public async Task CreateHoopsEntry_ShouldStoreUnpaid_WhenPlayerSendsPaidOnNewEntry()
        {
            var f = new Fixture();
            var body = MakeEntry("e1", "");
            body.IsPaid = true;

            await f.Build().CreateHoopsEntry(EndpointKit.Request(Tier.Player, body: body));

            f.Saved.Should().NotBeNull();
            f.Saved!.IsPaid.Should().BeFalse("only an Admin or SuperAdmin changes payment status");
        }

        // #99. A normal edit must leave a paid entry paid.
        [Fact]
        public async Task CreateHoopsEntry_ShouldKeepEntryPaid_WhenPlayerEditsPaidEntryWithoutPaidFlag()
        {
            var f = new Fixture();
            var stored = MakeEntry("e1", EndpointKit.PlayerId);
            stored.IsPaid = true;
            f.Stored(stored);
            var body = MakeEntry("e1", EndpointKit.PlayerId, pick: "Team B"); // IsPaid defaults to false

            await f.Build().CreateHoopsEntry(EndpointKit.Request(Tier.Player, body: body));

            f.Saved.Should().NotBeNull();
            f.Saved!.IsPaid.Should().BeTrue("the player's save must not change payment status");
        }

        // #131. An entry in an open pool must not be re-saved into a locked pool.
        [Fact]
        public async Task CreateHoopsEntry_ShouldRefuseMove_WhenPlayerMovesEntryFromOpenPoolToLockedPool()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId, "pool-open"));

            var result = await f.Build().CreateHoopsEntry(EndpointKit.Request(Tier.Player, body: MakeEntry("e1", EndpointKit.PlayerId, "pool-locked")));

            EndpointKit.Status(result).Should().BeOneOf(400, 403);
            f.NothingSaved();
        }

        // #131. An entry in a locked pool must not be moved into an open pool to change picks.
        [Fact]
        public async Task CreateHoopsEntry_ShouldRefuseMove_WhenPlayerMovesEntryFromLockedPoolToOpenPool()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId, "pool-locked", "Team A"));

            var result = await f.Build().CreateHoopsEntry(EndpointKit.Request(Tier.Player, body: MakeEntry("e1", EndpointKit.PlayerId, "pool-open", "Team B")));

            EndpointKit.Status(result).Should().BeOneOf(400, 403);
            f.NothingSaved();
        }

        // ── DeleteHoopsEntry (DELETE) ─────────────────────────────────────────────

        private const string DeleteQuery = "?seasonId=season-1";

        [Fact]
        public async Task DeleteHoopsEntry_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId));

            var result = await f.Build().DeleteHoopsEntry(EndpointKit.Request(Tier.Anonymous, DeleteQuery), "e1");

            EndpointKit.Status(result).Should().Be(401);
            f.NothingDeleted();
        }

        [Fact]
        public async Task DeleteHoopsEntry_ShouldDelete_WhenOwnerDeletesBeforeLock()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId));

            var result = await f.Build().DeleteHoopsEntry(EndpointKit.Request(Tier.Player, DeleteQuery), "e1");

            EndpointKit.Status(result).Should().Be(200);
            f.Entries.Verify(e => e.DeleteEntryAsync("e1", "season-1"), Times.Once);
        }

        [Fact]
        public async Task DeleteHoopsEntry_ShouldReturn403_WhenPlayerDeletesAnotherPlayersEntry()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.OtherPlayerId));

            var result = await f.Build().DeleteHoopsEntry(EndpointKit.Request(Tier.Player, DeleteQuery), "e1");

            EndpointKit.Status(result).Should().Be(403);
            f.NothingDeleted();
        }

        [Fact]
        public async Task DeleteHoopsEntry_ShouldRefuse_WhenOwnerDeletesAfterLock()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId, "pool-locked"));

            var result = await f.Build().DeleteHoopsEntry(EndpointKit.Request(Tier.Player, DeleteQuery), "e1");

            EndpointKit.Status(result).Should().Be(400);
            f.NothingDeleted();
        }

        [Theory]
        [InlineData(Tier.Admin, "pool-open")]
        [InlineData(Tier.Admin, "pool-locked")]
        [InlineData(Tier.SuperAdmin, "pool-open")]
        [InlineData(Tier.SuperAdmin, "pool-locked")]
        public async Task DeleteHoopsEntry_ShouldDelete_WhenCallerIsAdminOrAboveAtAnyTime(Tier tier, string poolId)
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId, poolId));

            var result = await f.Build().DeleteHoopsEntry(EndpointKit.Request(tier, DeleteQuery), "e1");

            EndpointKit.Status(result).Should().Be(200);
            f.Entries.Verify(e => e.DeleteEntryAsync("e1", "season-1"), Times.Once);
        }

        // ── SetHoopsEntryPaidStatus (POST) ────────────────────────────────────────

        private const string PaidQuery = "?id=e1&seasonId=season-1&isPaid=true";

        [Fact]
        public async Task SetHoopsEntryPaidStatus_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId));

            var result = await f.Build().SetHoopsEntryPaidStatus(EndpointKit.Request(Tier.Anonymous, PaidQuery));

            EndpointKit.Status(result).Should().Be(401);
            f.NothingSaved();
        }

        [Fact]
        public async Task SetHoopsEntryPaidStatus_ShouldReturn403_WhenCallerIsPlayer()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId)); // even the owner cannot mark their own entry paid

            var result = await f.Build().SetHoopsEntryPaidStatus(EndpointKit.Request(Tier.Player, PaidQuery));

            EndpointKit.Status(result).Should().Be(403);
            f.NothingSaved();
        }

        [Theory]
        [InlineData(Tier.Admin)]
        [InlineData(Tier.SuperAdmin)]
        public async Task SetHoopsEntryPaidStatus_ShouldMarkEntryPaid_WhenCallerIsAdminOrAbove(Tier tier)
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId));

            var result = await f.Build().SetHoopsEntryPaidStatus(EndpointKit.Request(tier, PaidQuery));

            EndpointKit.Status(result).Should().Be(200);
            f.Saved.Should().NotBeNull();
            f.Saved!.IsPaid.Should().BeTrue();
        }

        // ── Pick visibility on entry reads ────────────────────────────────────────

        private static Fixture ReadFixture(bool poolLocked)
        {
            var f = new Fixture();
            var entry = MakeEntry("e1", EndpointKit.OtherPlayerId, poolLocked ? "pool-locked" : "pool-open", SecretPick);
            f.Stored(entry);
            f.Entries.Setup(e => e.GetEntriesAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(new List<BracketEntry> { entry });
            return f;
        }

        // #97. Before lock, an anonymous caller must not see another player's picks.
        [Fact]
        public async Task GetHoopsEntry_ShouldNotReturnPicks_WhenAnonymousAndPoolNotLocked()
        {
            var f = ReadFixture(poolLocked: false);

            var result = await f.Build().GetHoopsEntry(EndpointKit.Request(Tier.Anonymous, "?id=e1&seasonId=season-1"));

            EndpointKit.BodyText(result).Should().NotContain(SecretPick);
        }

        // #97. Before lock, another signed-in player must not see the picks either.
        [Fact]
        public async Task GetHoopsEntry_ShouldNotReturnPicks_WhenCallerIsAnotherPlayerAndPoolNotLocked()
        {
            var f = ReadFixture(poolLocked: false);

            var result = await f.Build().GetHoopsEntry(EndpointKit.Request(Tier.Player, "?id=e1&seasonId=season-1"));

            EndpointKit.BodyText(result).Should().NotContain(SecretPick);
        }

        // #97. The list endpoint leaks picks before lock in the same way.
        [Fact]
        public async Task GetHoopsEntries_ShouldNotReturnPicks_WhenAnonymousAndPoolNotLocked()
        {
            var f = ReadFixture(poolLocked: false);

            var result = await f.Build().GetHoopsEntries(EndpointKit.Request(Tier.Anonymous, "?seasonId=season-1&poolId=pool-open"));

            EndpointKit.BodyText(result).Should().NotContain(SecretPick);
        }

        [Fact]
        public async Task GetHoopsEntry_ShouldReturnPicks_WhenCallerIsOwnerBeforeLock()
        {
            var f = ReadFixture(poolLocked: false);

            var result = await f.Build().GetHoopsEntry(EndpointKit.Request(EndpointKit.OtherPlayerId, "?id=e1&seasonId=season-1"));

            EndpointKit.BodyText(result).Should().Contain(SecretPick);
        }

        [Theory]
        [InlineData(Tier.Admin)]
        [InlineData(Tier.SuperAdmin)]
        public async Task GetHoopsEntry_ShouldReturnPicks_WhenCallerIsAdminOrAboveBeforeLock(Tier tier)
        {
            var f = ReadFixture(poolLocked: false);

            var result = await f.Build().GetHoopsEntry(EndpointKit.Request(tier, "?id=e1&seasonId=season-1"));

            EndpointKit.BodyText(result).Should().Contain(SecretPick);
        }

        [Fact]
        public async Task GetHoopsEntry_ShouldReturnPicks_WhenAnonymousAndPoolLocked()
        {
            var f = ReadFixture(poolLocked: true);

            var result = await f.Build().GetHoopsEntry(EndpointKit.Request(Tier.Anonymous, "?id=e1&seasonId=season-1"));

            EndpointKit.BodyText(result).Should().Contain(SecretPick);
        }

        [Fact]
        public async Task GetMyHoopsEntries_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();

            var result = await f.Build().GetMyHoopsEntries(EndpointKit.Request(Tier.Anonymous, "?seasonId=season-1"));

            EndpointKit.Status(result).Should().Be(401);
            f.Entries.Verify(e => e.GetEntriesForUserAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
        }
    }

    /// <summary>
    /// Hoops pool and archive endpoints: BUSINESS-RULES section 1 gives create, edit, delete, conclude
    /// and archive of pools to SuperAdmin only.
    /// </summary>
    public class EndpointAuthHoopsPoolTests
    {
        private class Fixture
        {
            public Mock<IHoopsPoolRepository> Pools = new();
            public Mock<IHoopsGameRepository> Games = new();
            public Mock<IHoopsEntryRepository> Entries = new();
            public Mock<IUserRepository> Users = EndpointKit.Users();

            public Fixture()
            {
                Pools.Setup(p => p.GetPoolAsync(It.IsAny<string>())).ReturnsAsync(NewPool());
                Entries.Setup(e => e.GetEntriesAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(new List<BracketEntry>());
            }

            public HoopsPoolFunctions Build() => new(new NullLoggerFactory(), Pools.Object, Games.Object, Entries.Object, Users.Object);
        }

        private static HoopsPool NewPool() => new()
        {
            Id = "pool-1", SeasonId = "season-1", Name = "Family", InviteCode = "code", LockDate = DateTime.UtcNow.AddDays(7)
        };

        // ── CreateHoopsPool (POST) ────────────────────────────────────────────────

        [Fact]
        public async Task CreateHoopsPool_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();

            var result = await f.Build().CreateHoopsPool(EndpointKit.Request(Tier.Anonymous, body: NewPool()));

            EndpointKit.Status(result).Should().Be(401);
            f.Pools.Verify(p => p.AddPoolAsync(It.IsAny<HoopsPool>()), Times.Never);
        }

        [Theory]
        [InlineData(Tier.Player)]
        [InlineData(Tier.Admin)]
        public async Task CreateHoopsPool_ShouldReturn403_WhenCallerIsNotSuperAdmin(Tier tier)
        {
            var f = new Fixture();

            var result = await f.Build().CreateHoopsPool(EndpointKit.Request(tier, body: NewPool()));

            EndpointKit.Status(result).Should().Be(403);
            f.Pools.Verify(p => p.AddPoolAsync(It.IsAny<HoopsPool>()), Times.Never);
        }

        [Fact]
        public async Task CreateHoopsPool_ShouldSavePool_WhenSuperAdmin()
        {
            var f = new Fixture();

            var result = await f.Build().CreateHoopsPool(EndpointKit.Request(Tier.SuperAdmin, body: NewPool()));

            EndpointKit.Status(result).Should().Be(200);
            f.Pools.Verify(p => p.AddPoolAsync(It.IsAny<HoopsPool>()), Times.Once);
        }

        // ── UpdateHoopsPool (PUT) ─────────────────────────────────────────────────

        [Fact]
        public async Task UpdateHoopsPool_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();

            var result = await f.Build().UpdateHoopsPool(EndpointKit.Request(Tier.Anonymous, body: NewPool()));

            EndpointKit.Status(result).Should().Be(401);
            f.Pools.Verify(p => p.AddPoolAsync(It.IsAny<HoopsPool>()), Times.Never);
        }

        [Theory]
        [InlineData(Tier.Player)]
        [InlineData(Tier.Admin)]
        public async Task UpdateHoopsPool_ShouldReturn403_WhenCallerIsNotSuperAdmin(Tier tier)
        {
            var f = new Fixture();

            var result = await f.Build().UpdateHoopsPool(EndpointKit.Request(tier, body: NewPool()));

            EndpointKit.Status(result).Should().Be(403);
            f.Pools.Verify(p => p.AddPoolAsync(It.IsAny<HoopsPool>()), Times.Never);
        }

        [Fact]
        public async Task UpdateHoopsPool_ShouldSavePool_WhenSuperAdmin()
        {
            var f = new Fixture();

            var result = await f.Build().UpdateHoopsPool(EndpointKit.Request(Tier.SuperAdmin, body: NewPool()));

            EndpointKit.Status(result).Should().Be(200);
            f.Pools.Verify(p => p.AddPoolAsync(It.IsAny<HoopsPool>()), Times.Once);
        }

        // ── DeleteHoopsPool (DELETE) ──────────────────────────────────────────────

        [Fact]
        public async Task DeleteHoopsPool_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();

            var result = await f.Build().DeleteHoopsPool(EndpointKit.Request(Tier.Anonymous), "pool-1");

            EndpointKit.Status(result).Should().Be(401);
            f.Pools.Verify(p => p.DeletePoolAsync(It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData(Tier.Player)]
        [InlineData(Tier.Admin)]
        public async Task DeleteHoopsPool_ShouldReturn403_WhenCallerIsNotSuperAdmin(Tier tier)
        {
            var f = new Fixture();

            var result = await f.Build().DeleteHoopsPool(EndpointKit.Request(tier), "pool-1");

            EndpointKit.Status(result).Should().Be(403);
            f.Pools.Verify(p => p.DeletePoolAsync(It.IsAny<string>()), Times.Never);
            f.Entries.Verify(e => e.DeleteEntryAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DeleteHoopsPool_ShouldDeletePool_WhenSuperAdmin()
        {
            var f = new Fixture();

            var result = await f.Build().DeleteHoopsPool(EndpointKit.Request(Tier.SuperAdmin), "pool-1");

            EndpointKit.Status(result).Should().Be(200);
            f.Pools.Verify(p => p.DeletePoolAsync("pool-1"), Times.Once);
        }

        // ── ToggleHoopsConclusion (POST) ──────────────────────────────────────────

        [Fact]
        public async Task ToggleHoopsConclusion_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();

            var result = await f.Build().ToggleHoopsConclusion(EndpointKit.Request(Tier.Anonymous), "pool-1");

            EndpointKit.Status(result).Should().Be(401);
            f.Pools.Verify(p => p.AddPoolAsync(It.IsAny<HoopsPool>()), Times.Never);
        }

        [Theory]
        [InlineData(Tier.Player)]
        [InlineData(Tier.Admin)]
        public async Task ToggleHoopsConclusion_ShouldReturn403_WhenCallerIsNotSuperAdmin(Tier tier)
        {
            var f = new Fixture();

            var result = await f.Build().ToggleHoopsConclusion(EndpointKit.Request(tier), "pool-1");

            EndpointKit.Status(result).Should().Be(403);
            f.Pools.Verify(p => p.AddPoolAsync(It.IsAny<HoopsPool>()), Times.Never);
        }

        [Fact]
        public async Task ToggleHoopsConclusion_ShouldFlipConcludedFlag_WhenSuperAdmin()
        {
            var f = new Fixture();

            var result = await f.Build().ToggleHoopsConclusion(EndpointKit.Request(Tier.SuperAdmin), "pool-1");

            EndpointKit.Status(result).Should().Be(200);
            f.Pools.Verify(p => p.AddPoolAsync(It.Is<HoopsPool>(x => x.IsConcluded)), Times.Once);
        }

        // ── ArchiveHoopsPool (POST) ───────────────────────────────────────────────
        // HoopsArchiveFunctionsTests covers anonymous and Admin; Player and the success side are added here.

        private static HoopsArchiveFunctions BuildArchive(Mock<IHoopsPoolRepository> pools, Mock<IArchiveRepository> archives, Mock<IUserRepository> users) =>
            new(new NullLoggerFactory(), pools.Object, new Mock<IHoopsGameRepository>().Object,
                new Mock<IHoopsEntryRepository>().Object, archives.Object, users.Object);

        [Theory]
        [InlineData(Tier.Anonymous, 401)]
        [InlineData(Tier.Player, 403)]
        [InlineData(Tier.Admin, 403)]
        public async Task ArchiveHoopsPool_ShouldRefuseAndArchiveNothing_WhenCallerIsNotSuperAdmin(Tier tier, int expected)
        {
            var pools = new Mock<IHoopsPoolRepository>();
            var archives = new Mock<IArchiveRepository>();

            var result = await BuildArchive(pools, archives, EndpointKit.Users()).ArchiveHoopsPool(EndpointKit.Request(tier), "pool-1");

            EndpointKit.Status(result).Should().Be(expected);
            pools.Verify(p => p.GetPoolAsync(It.IsAny<string>()), Times.Never);
            archives.Verify(a => a.AddArchiveAsync(It.IsAny<PoolArchive>()), Times.Never);
        }

        [Fact]
        public async Task ArchiveHoopsPool_ShouldReachPoolLookup_WhenSuperAdmin()
        {
            var pools = new Mock<IHoopsPoolRepository>();
            pools.Setup(p => p.GetPoolAsync(It.IsAny<string>())).ReturnsAsync((HoopsPool?)null);

            var result = await BuildArchive(pools, new Mock<IArchiveRepository>(), EndpointKit.Users()).ArchiveHoopsPool(EndpointKit.Request(Tier.SuperAdmin), "pool-1");

            new[] { 401, 403 }.Should().NotContain(EndpointKit.Status(result));
            pools.Verify(p => p.GetPoolAsync(It.IsAny<string>()), Times.Once);
        }
    }

    /// <summary>
    /// Hoops game and bracket endpoints. The business rules put creating and editing games and
    /// brackets with SuperAdmin, but whether Admin may enter results and link games is undecided (#104),
    /// so these tests do not assert the Admin tier.
    /// </summary>
    public class EndpointAuthHoopsGameTests
    {
        private class Fixture
        {
            public Mock<IHoopsGameRepository> Games = new();
            public Mock<IHoopsPoolRepository> Pools = new();
            public Mock<IUserRepository> Users = EndpointKit.Users();
            public Mock<IBracketGeneratorService> Generator = new();
            public Mock<IHoopsGameScoringService> Scoring = new();

            public Fixture()
            {
                Pools.Setup(p => p.GetPoolAsync(It.IsAny<string>())).ReturnsAsync(new HoopsPool { Id = "pool-1", SeasonId = "season-1" });
                Generator.Setup(g => g.GenerateBracket(It.IsAny<BracketGenerationRequest>())).Returns(new List<HoopsGame> { NewGame() });
            }

            public HoopsGameFunctions Build() => new(
                new NullLoggerFactory(), Games.Object, Pools.Object, Users.Object, Generator.Object, Scoring.Object,
                new Mock<IEspnDataService>().Object, new Mock<IBasketballDataService>().Object);
        }

        private static HoopsGame NewGame() => new() { Id = "hg-1", SeasonId = "season-1" };

        private static BracketGenerationRequest NewRequest() => new() { PoolId = "pool-1", SeasonId = "season-1" };

        // ── GenerateBracket (POST) ────────────────────────────────────────────────

        [Fact]
        public async Task GenerateBracket_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();

            var result = await f.Build().GenerateBracket(EndpointKit.Request(Tier.Anonymous, body: NewRequest()));

            EndpointKit.Status(result).Should().Be(401);
            f.Games.Verify(g => g.SaveGamesAsBatchAsync(It.IsAny<List<HoopsGame>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GenerateBracket_ShouldReturn403_WhenCallerIsPlayer()
        {
            var f = new Fixture();

            var result = await f.Build().GenerateBracket(EndpointKit.Request(Tier.Player, body: NewRequest()));

            EndpointKit.Status(result).Should().Be(403);
            f.Games.Verify(g => g.SaveGamesAsBatchAsync(It.IsAny<List<HoopsGame>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GenerateBracket_ShouldSaveGames_WhenSuperAdmin()
        {
            var f = new Fixture();

            var result = await f.Build().GenerateBracket(EndpointKit.Request(Tier.SuperAdmin, body: NewRequest()));

            EndpointKit.Status(result).Should().Be(200);
            f.Games.Verify(g => g.SaveGamesAsBatchAsync(It.IsAny<List<HoopsGame>>(), It.IsAny<string>()), Times.Once);
        }

        // ── UpdateHoopsGame (PUT) ─────────────────────────────────────────────────

        [Fact]
        public async Task UpdateHoopsGame_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();

            var result = await f.Build().UpdateHoopsGame(EndpointKit.Request(Tier.Anonymous, body: NewGame()));

            EndpointKit.Status(result).Should().Be(401);
            f.Games.Verify(g => g.UpdateGameAsync(It.IsAny<HoopsGame>()), Times.Never);
        }

        [Fact]
        public async Task UpdateHoopsGame_ShouldReturn403_WhenCallerIsPlayer()
        {
            var f = new Fixture();

            var result = await f.Build().UpdateHoopsGame(EndpointKit.Request(Tier.Player, body: NewGame()));

            EndpointKit.Status(result).Should().Be(403);
            f.Games.Verify(g => g.UpdateGameAsync(It.IsAny<HoopsGame>()), Times.Never);
        }

        [Fact]
        public async Task UpdateHoopsGame_ShouldUpdateGame_WhenSuperAdmin()
        {
            var f = new Fixture();

            var result = await f.Build().UpdateHoopsGame(EndpointKit.Request(Tier.SuperAdmin, body: NewGame()));

            EndpointKit.Status(result).Should().Be(200);
            f.Games.Verify(g => g.UpdateGameAsync(It.IsAny<HoopsGame>()), Times.Once);
        }

        // ── SaveHoopsTeamAssignments (POST) ───────────────────────────────────────

        [Fact]
        public async Task SaveHoopsTeamAssignments_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();

            var result = await f.Build().SaveHoopsTeamAssignments(EndpointKit.Request(Tier.Anonymous, body: new List<HoopsGame> { NewGame() }));

            EndpointKit.Status(result).Should().Be(401);
            f.Games.Verify(g => g.SaveGamesAsBatchAsync(It.IsAny<List<HoopsGame>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SaveHoopsTeamAssignments_ShouldReturn403_WhenCallerIsPlayer()
        {
            var f = new Fixture();

            var result = await f.Build().SaveHoopsTeamAssignments(EndpointKit.Request(Tier.Player, body: new List<HoopsGame> { NewGame() }));

            EndpointKit.Status(result).Should().Be(403);
            f.Games.Verify(g => g.SaveGamesAsBatchAsync(It.IsAny<List<HoopsGame>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SaveHoopsTeamAssignments_ShouldSaveGames_WhenSuperAdmin()
        {
            var f = new Fixture();

            var result = await f.Build().SaveHoopsTeamAssignments(EndpointKit.Request(Tier.SuperAdmin, body: new List<HoopsGame> { NewGame() }));

            EndpointKit.Status(result).Should().Be(200);
            f.Games.Verify(g => g.SaveGamesAsBatchAsync(It.IsAny<List<HoopsGame>>(), "season-1"), Times.Once);
        }

        // ── DeleteHoopsGame (DELETE) ──────────────────────────────────────────────

        [Fact]
        public async Task DeleteHoopsGame_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();

            var result = await f.Build().DeleteHoopsGame(EndpointKit.Request(Tier.Anonymous, "?seasonId=season-1"), "hg-1");

            EndpointKit.Status(result).Should().Be(401);
            f.Games.Verify(g => g.DeleteGameAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DeleteHoopsGame_ShouldReturn403_WhenCallerIsPlayer()
        {
            var f = new Fixture();

            var result = await f.Build().DeleteHoopsGame(EndpointKit.Request(Tier.Player, "?seasonId=season-1"), "hg-1");

            EndpointKit.Status(result).Should().Be(403);
            f.Games.Verify(g => g.DeleteGameAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DeleteHoopsGame_ShouldDeleteGame_WhenSuperAdmin()
        {
            var f = new Fixture();

            var result = await f.Build().DeleteHoopsGame(EndpointKit.Request(Tier.SuperAdmin, "?seasonId=season-1"), "hg-1");

            EndpointKit.Status(result).Should().Be(200);
            f.Games.Verify(g => g.DeleteGameAsync("hg-1", "season-1"), Times.Once);
        }

        // ── SaveHoopsGame (POST) ──────────────────────────────────────────────────

        [Fact]
        public async Task SaveHoopsGame_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();

            var result = await f.Build().SaveHoopsGame(EndpointKit.Request(Tier.Anonymous, body: NewGame()));

            EndpointKit.Status(result).Should().Be(401);
            f.Scoring.Verify(s => s.ProcessGameUpdateAsync(It.IsAny<HoopsGame>()), Times.Never);
        }

        [Fact]
        public async Task SaveHoopsGame_ShouldReturn403_WhenCallerIsPlayer()
        {
            var f = new Fixture();

            var result = await f.Build().SaveHoopsGame(EndpointKit.Request(Tier.Player, body: NewGame()));

            EndpointKit.Status(result).Should().Be(403);
            f.Scoring.Verify(s => s.ProcessGameUpdateAsync(It.IsAny<HoopsGame>()), Times.Never);
        }

        [Fact]
        public async Task SaveHoopsGame_ShouldProcessGame_WhenSuperAdmin()
        {
            var f = new Fixture();

            var result = await f.Build().SaveHoopsGame(EndpointKit.Request(Tier.SuperAdmin, body: NewGame()));

            EndpointKit.Status(result).Should().Be(200);
            f.Scoring.Verify(s => s.ProcessGameUpdateAsync(It.IsAny<HoopsGame>()), Times.Once);
        }

        // ── ForceHoopsPropagation (POST) ──────────────────────────────────────────

        [Fact]
        public async Task ForceHoopsPropagation_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();

            var result = await f.Build().ForceHoopsPropagation(EndpointKit.Request(Tier.Anonymous, "?seasonId=season-1"));

            EndpointKit.Status(result).Should().Be(401);
            f.Scoring.Verify(s => s.ForcePropagateAllAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ForceHoopsPropagation_ShouldReturn403_WhenCallerIsPlayer()
        {
            var f = new Fixture();

            var result = await f.Build().ForceHoopsPropagation(EndpointKit.Request(Tier.Player, "?seasonId=season-1"));

            EndpointKit.Status(result).Should().Be(403);
            f.Scoring.Verify(s => s.ForcePropagateAllAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ForceHoopsPropagation_ShouldPropagate_WhenSuperAdmin()
        {
            var f = new Fixture();

            var result = await f.Build().ForceHoopsPropagation(EndpointKit.Request(Tier.SuperAdmin, "?seasonId=season-1"));

            EndpointKit.Status(result).Should().Be(200);
            f.Scoring.Verify(s => s.ForcePropagateAllAsync("season-1"), Times.Once);
        }
    }
}
