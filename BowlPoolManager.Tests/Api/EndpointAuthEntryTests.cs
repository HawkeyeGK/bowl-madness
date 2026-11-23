using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BowlPoolManager.Api.Functions;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// Sign-in, owner and role tests for the football entry endpoints, against BUSINESS-RULES
    /// sections 1 and 3: an owner changes or deletes their own entry until lock, an Admin or
    /// SuperAdmin ignores every lock, payment status is Admin only, and nobody but the owner and
    /// Admins sees picks before lock. Tests that name an issue describe the correct behavior that
    /// the code once lacked.
    /// </summary>
    public class EndpointAuthFootballEntryTests
    {
        private const string SecretPick = "SecretPickU";

        private class Fixture
        {
            public Mock<IEntryRepository> Entries = new();
            public Mock<IPoolRepository> Pools = new();
            public Mock<IGameRepository> Games = new();
            public Mock<IUserRepository> Users = EndpointKit.Users();
            public BowlPool OpenPool = MakePool("pool-open", locked: false);
            public BowlPool LockedPool = MakePool("pool-locked", locked: true);
            public BracketEntry? Saved;

            public Fixture()
            {
                Pools.Setup(p => p.GetPoolAsync("pool-open")).ReturnsAsync(OpenPool);
                Pools.Setup(p => p.GetPoolAsync("pool-locked")).ReturnsAsync(LockedPool);
                Games.Setup(g => g.GetGamesAsync(It.IsAny<string?>())).ReturnsAsync(new List<BowlGame> { new() { Id = "game-1" } });
                Entries.Setup(e => e.IsBracketNameTakenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(false);
                Entries.Setup(e => e.AddEntryAsync(It.IsAny<BracketEntry>()))
                    .Callback<BracketEntry>(e => Saved = e)
                    .Returns(Task.CompletedTask);
            }

            public void Stored(BracketEntry entry) =>
                Entries.Setup(e => e.GetEntryAsync(entry.Id, entry.SeasonId)).ReturnsAsync(entry);

            public EntryFunctions Build() => new(new NullLoggerFactory(), Entries.Object, Pools.Object, Games.Object, Users.Object);

            public void NothingSaved() => Entries.Verify(e => e.AddEntryAsync(It.IsAny<BracketEntry>()), Times.Never);
            public void NothingDeleted() => Entries.Verify(e => e.DeleteEntryAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        private static BowlPool MakePool(string id, bool locked) => new()
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

        // ── CreateEntry (POST) ────────────────────────────────────────────────────

        [Fact]
        public async Task CreateEntry_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Anonymous, body: MakeEntry("e1", "")));

            EndpointKit.Status(result).Should().Be(401);
            f.NothingSaved();
        }

        [Fact]
        public async Task CreateEntry_ShouldRecordEntryAsCallers_WhenPlayerSavesBeforeLock()
        {
            var f = new Fixture();
            var body = MakeEntry("e1", EndpointKit.OtherPlayerId); // a player cannot save an entry as someone else

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: body));

            EndpointKit.Status(result).Should().Be(200);
            f.Saved.Should().NotBeNull();
            f.Saved!.UserId.Should().Be(EndpointKit.PlayerId);
        }

        [Fact]
        public async Task CreateEntry_ShouldReturn403_WhenPlayerEditsAnotherPlayersEntry()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.OtherPlayerId));

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: MakeEntry("e1", EndpointKit.OtherPlayerId, pick: "Team B")));

            EndpointKit.Status(result).Should().Be(403);
            f.NothingSaved();
        }

        [Fact]
        public async Task CreateEntry_ShouldRefuseNewEntry_WhenPlayerJoinsLockedPool()
        {
            var f = new Fixture();

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: MakeEntry("e1", "", "pool-locked")));

            EndpointKit.Status(result).Should().Be(400);
            f.NothingSaved();
        }

        [Fact]
        public async Task CreateEntry_ShouldRefusePickChange_WhenPlayerEditsOwnEntryInLockedPool()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId, "pool-locked", "Team A"));

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: MakeEntry("e1", EndpointKit.PlayerId, "pool-locked", "Team B")));

            EndpointKit.Status(result).Should().Be(400);
            f.NothingSaved();
        }

        [Fact]
        public async Task CreateEntry_ShouldAllowRename_WhenPlayerRenamesOwnEntryInLockedPool()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId, "pool-locked", "Team A"));
            var body = MakeEntry("e1", EndpointKit.PlayerId, "pool-locked", "Team A");
            body.PlayerName = "A better name";

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: body));

            EndpointKit.Status(result).Should().Be(200);
            f.Saved!.PlayerName.Should().Be("A better name");
        }

        [Theory]
        [InlineData(Tier.Admin)]
        [InlineData(Tier.SuperAdmin)]
        public async Task CreateEntry_ShouldSaveAnotherPlayersEntry_WhenCallerIsAdminOrAbove(Tier tier)
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId));

            var result = await f.Build().CreateEntry(EndpointKit.Request(tier, body: MakeEntry("e1", EndpointKit.PlayerId, pick: "Team B")));

            EndpointKit.Status(result).Should().Be(200);
            f.Saved.Should().NotBeNull();
            f.Saved!.UserId.Should().Be(EndpointKit.PlayerId, "an Admin edit keeps the owner unless assignToMe is set");
        }

        [Theory]
        [InlineData(Tier.Admin)]
        [InlineData(Tier.SuperAdmin)]
        public async Task CreateEntry_ShouldIgnoreLock_WhenCallerIsAdminOrAbove(Tier tier)
        {
            var f = new Fixture();

            var result = await f.Build().CreateEntry(EndpointKit.Request(tier, body: MakeEntry("e1", EndpointKit.PlayerId, "pool-locked")));

            EndpointKit.Status(result).Should().Be(200);
            f.Saved.Should().NotBeNull();
        }

        // #99. A player's save must never set payment status.
        [Fact]
        public async Task CreateEntry_ShouldStoreUnpaid_WhenPlayerSendsPaidOnNewEntry()
        {
            var f = new Fixture();
            var body = MakeEntry("e1", "");
            body.IsPaid = true;

            await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: body));

            f.Saved.Should().NotBeNull();
            f.Saved!.IsPaid.Should().BeFalse("only an Admin or SuperAdmin changes payment status");
        }

        // #99. A normal edit must leave a paid entry paid.
        [Fact]
        public async Task CreateEntry_ShouldKeepEntryPaid_WhenPlayerEditsPaidEntryWithoutPaidFlag()
        {
            var f = new Fixture();
            var stored = MakeEntry("e1", EndpointKit.PlayerId);
            stored.IsPaid = true;
            f.Stored(stored);
            var body = MakeEntry("e1", EndpointKit.PlayerId, pick: "Team B"); // IsPaid defaults to false

            await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: body));

            f.Saved.Should().NotBeNull();
            f.Saved!.IsPaid.Should().BeTrue("the player's save must not change payment status");
        }

        // #131. An entry in an open pool must not be re-saved into a locked pool.
        [Fact]
        public async Task CreateEntry_ShouldRefuseMove_WhenPlayerMovesEntryFromOpenPoolToLockedPool()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId, "pool-open"));

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: MakeEntry("e1", EndpointKit.PlayerId, "pool-locked")));

            EndpointKit.Status(result).Should().BeOneOf(400, 403);
            f.NothingSaved();
        }

        // #131. An entry in a locked pool must not be moved into an open pool to change picks.
        [Fact]
        public async Task CreateEntry_ShouldRefuseMove_WhenPlayerMovesEntryFromLockedPoolToOpenPool()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId, "pool-locked", "Team A"));

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: MakeEntry("e1", EndpointKit.PlayerId, "pool-open", "Team B")));

            EndpointKit.Status(result).Should().BeOneOf(400, 403);
            f.NothingSaved();
        }

        // ── DeleteEntry (DELETE) ──────────────────────────────────────────────────

        private const string DeleteQuery = "?seasonId=season-1";

        [Fact]
        public async Task DeleteEntry_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId));

            var result = await f.Build().DeleteEntry(EndpointKit.Request(Tier.Anonymous, DeleteQuery), "e1");

            EndpointKit.Status(result).Should().Be(401);
            f.NothingDeleted();
        }

        [Fact]
        public async Task DeleteEntry_ShouldDelete_WhenOwnerDeletesBeforeLock()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId));

            var result = await f.Build().DeleteEntry(EndpointKit.Request(Tier.Player, DeleteQuery), "e1");

            EndpointKit.Status(result).Should().Be(200);
            f.Entries.Verify(e => e.DeleteEntryAsync("e1", "season-1"), Times.Once);
        }

        [Fact]
        public async Task DeleteEntry_ShouldReturn403_WhenPlayerDeletesAnotherPlayersEntry()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.OtherPlayerId));

            var result = await f.Build().DeleteEntry(EndpointKit.Request(Tier.Player, DeleteQuery), "e1");

            EndpointKit.Status(result).Should().Be(403);
            f.NothingDeleted();
        }

        [Fact]
        public async Task DeleteEntry_ShouldRefuse_WhenOwnerDeletesAfterLock()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId, "pool-locked"));

            var result = await f.Build().DeleteEntry(EndpointKit.Request(Tier.Player, DeleteQuery), "e1");

            EndpointKit.Status(result).Should().Be(400);
            f.NothingDeleted();
        }

        [Theory]
        [InlineData(Tier.Admin, "pool-open")]
        [InlineData(Tier.Admin, "pool-locked")]
        [InlineData(Tier.SuperAdmin, "pool-open")]
        [InlineData(Tier.SuperAdmin, "pool-locked")]
        public async Task DeleteEntry_ShouldDelete_WhenCallerIsAdminOrAboveAtAnyTime(Tier tier, string poolId)
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId, poolId));

            var result = await f.Build().DeleteEntry(EndpointKit.Request(tier, DeleteQuery), "e1");

            EndpointKit.Status(result).Should().Be(200);
            f.Entries.Verify(e => e.DeleteEntryAsync("e1", "season-1"), Times.Once);
        }

        // ── SetEntryPaidStatus (POST) ─────────────────────────────────────────────

        private const string PaidQuery = "?id=e1&seasonId=season-1&isPaid=true";

        [Fact]
        public async Task SetEntryPaidStatus_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId));

            var result = await f.Build().SetEntryPaidStatus(EndpointKit.Request(Tier.Anonymous, PaidQuery));

            EndpointKit.Status(result).Should().Be(401);
            f.NothingSaved();
        }

        [Fact]
        public async Task SetEntryPaidStatus_ShouldReturn403_WhenCallerIsPlayer()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId)); // even the owner cannot mark their own entry paid

            var result = await f.Build().SetEntryPaidStatus(EndpointKit.Request(Tier.Player, PaidQuery));

            EndpointKit.Status(result).Should().Be(403);
            f.NothingSaved();
        }

        [Theory]
        [InlineData(Tier.Admin)]
        [InlineData(Tier.SuperAdmin)]
        public async Task SetEntryPaidStatus_ShouldMarkEntryPaid_WhenCallerIsAdminOrAbove(Tier tier)
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId));

            var result = await f.Build().SetEntryPaidStatus(EndpointKit.Request(tier, PaidQuery));

            EndpointKit.Status(result).Should().Be(200);
            f.Saved.Should().NotBeNull();
            f.Saved!.IsPaid.Should().BeTrue();
        }

        // ── Pick visibility on entry reads ────────────────────────────────────────

        private static Fixture ReadFixture(bool poolLocked, out BracketEntry entry)
        {
            var f = new Fixture();
            entry = MakeEntry("e1", EndpointKit.OtherPlayerId, poolLocked ? "pool-locked" : "pool-open", SecretPick);
            f.Stored(entry);
            var stored = entry;
            f.Entries.Setup(e => e.GetEntriesAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(new List<BracketEntry> { stored });
            return f;
        }

        // #97. Before lock, an anonymous caller must not see another player's picks.
        [Fact]
        public async Task GetEntry_ShouldNotReturnPicks_WhenAnonymousAndPoolNotLocked()
        {
            var f = ReadFixture(poolLocked: false, out _);

            var result = await f.Build().GetEntry(EndpointKit.Request(Tier.Anonymous, "?id=e1&seasonId=season-1"));

            EndpointKit.BodyText(result).Should().NotContain(SecretPick);
        }

        // #97. Before lock, another signed-in player must not see the picks either.
        [Fact]
        public async Task GetEntry_ShouldNotReturnPicks_WhenCallerIsAnotherPlayerAndPoolNotLocked()
        {
            var f = ReadFixture(poolLocked: false, out _);

            var result = await f.Build().GetEntry(EndpointKit.Request(Tier.Player, "?id=e1&seasonId=season-1"));

            EndpointKit.BodyText(result).Should().NotContain(SecretPick);
        }

        // #97. The list endpoint leaks picks before lock in the same way.
        [Fact]
        public async Task GetEntries_ShouldNotReturnPicks_WhenAnonymousAndPoolNotLocked()
        {
            var f = ReadFixture(poolLocked: false, out _);

            var result = await f.Build().GetEntries(EndpointKit.Request(Tier.Anonymous, "?seasonId=season-1&poolId=pool-open"));

            EndpointKit.BodyText(result).Should().NotContain(SecretPick);
        }

        [Fact]
        public async Task GetEntry_ShouldReturnPicks_WhenCallerIsOwnerBeforeLock()
        {
            var f = ReadFixture(poolLocked: false, out _);

            var result = await f.Build().GetEntry(EndpointKit.Request(EndpointKit.OtherPlayerId, "?id=e1&seasonId=season-1"));

            EndpointKit.BodyText(result).Should().Contain(SecretPick);
        }

        [Theory]
        [InlineData(Tier.Admin)]
        [InlineData(Tier.SuperAdmin)]
        public async Task GetEntry_ShouldReturnPicks_WhenCallerIsAdminOrAboveBeforeLock(Tier tier)
        {
            var f = ReadFixture(poolLocked: false, out _);

            var result = await f.Build().GetEntry(EndpointKit.Request(tier, "?id=e1&seasonId=season-1"));

            EndpointKit.BodyText(result).Should().Contain(SecretPick);
        }

        [Fact]
        public async Task GetEntry_ShouldReturnPicks_WhenAnonymousAndPoolLocked()
        {
            var f = ReadFixture(poolLocked: true, out _);

            var result = await f.Build().GetEntry(EndpointKit.Request(Tier.Anonymous, "?id=e1&seasonId=season-1"));

            EndpointKit.BodyText(result).Should().Contain(SecretPick);
        }

        // ── GetMyEntries (own entries only) ───────────────────────────────────────

        [Fact]
        public async Task GetMyEntries_ShouldReturn401_WhenAnonymous()
        {
            var f = new Fixture();

            var result = await f.Build().GetMyEntries(EndpointKit.Request(Tier.Anonymous, "?seasonId=season-1"));

            EndpointKit.Status(result).Should().Be(401);
            f.Entries.Verify(e => e.GetEntriesForUserAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
        }
    }
}
