using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BowlPoolManager.Api.Functions;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// Group B entry rules for football: what a save may set (#99, #134), moving between pools (#131) and what
    /// the read endpoints return to whom (#97). BUSINESS-RULES section 3.
    /// </summary>
    public class FootballEntryRulesTests
    {
        private const string SecretPick = "SecretPickU";

        private class Fixture
        {
            public Mock<IEntryRepository> Entries = new();
            public Mock<IPoolRepository> Pools = new();
            public Mock<IGameRepository> Games = new();
            public Mock<IUserRepository> Users = EndpointKit.Users();
            public BracketEntry? Saved;

            public Fixture()
            {
                Pools.Setup(p => p.GetPoolAsync("pool-open")).ReturnsAsync(MakePool("pool-open", locked: false));
                Pools.Setup(p => p.GetPoolAsync("pool-open-2")).ReturnsAsync(MakePool("pool-open-2", locked: false));
                Pools.Setup(p => p.GetPoolAsync("pool-locked")).ReturnsAsync(MakePool("pool-locked", locked: true));
                Games.Setup(g => g.GetGamesAsync(It.IsAny<string?>())).ReturnsAsync(new List<BowlGame> { new() { Id = "game-1", BowlName = "First Bowl", TeamHome = "Team A", TeamAway = "Team B" } });
                Entries.Setup(e => e.IsBracketNameTakenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(false);
                Entries.Setup(e => e.AddEntryAsync(It.IsAny<BracketEntry>()))
                    .Callback<BracketEntry>(e => Saved = e)
                    .Returns(Task.CompletedTask);
            }

            public void Stored(BracketEntry entry) =>
                Entries.Setup(e => e.GetEntryAsync(entry.Id, entry.SeasonId)).ReturnsAsync(entry);

            public void Listed(params BracketEntry[] entries) =>
                Entries.Setup(e => e.GetEntriesAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(entries.ToList());

            public EntryFunctions Build() => new(new NullLoggerFactory(), Entries.Object, Pools.Object, Games.Object, Users.Object);
        }

        private static BowlPool MakePool(string id, bool locked) => new()
        {
            Id = id, SeasonId = "season-1", Name = id, InviteCode = "code",
            LockDate = locked ? DateTime.UtcNow.AddDays(-1) : DateTime.UtcNow.AddDays(7)
        };

        private static BracketEntry MakeEntry(string id, string userId, string poolId = "pool-open", string pick = "Team A") => new()
        {
            Id = id, SeasonId = "season-1", PoolId = poolId, UserId = userId,
            PlayerName = "Bracket " + id,
            Picks = new Dictionary<string, string> { ["game-1"] = pick },
            TieBreakerPoints = 55,
            AuditLog = new List<string> { "Entry Created by owner@test.com on Dec 1" }
        };

        // ── #134: the server owns everything but name, picks and tiebreaker ──────

        [Fact]
        public async Task CreateEntry_ShouldIgnoreServerOwnedFields_WhenPlayerSendsThem()
        {
            var f = new Fixture();
            var body = MakeEntry("e1", EndpointKit.OtherPlayerId);
            body.IsAdminEntered = true;
            body.IsPaid = true;
            body.CreatedOn = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            body.Type = "Something Else";
            body.AuditLog = new List<string> { "forged line" };
            body.SeasonId = "another-season";

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: body));

            EndpointKit.Status(result).Should().Be(200);
            f.Saved!.IsAdminEntered.Should().BeFalse();
            f.Saved.IsPaid.Should().BeFalse();
            f.Saved.CreatedOn.Should().BeAfter(new DateTime(2020, 1, 1));
            f.Saved.Type.Should().Be(Constants.DocumentTypes.BracketEntry);
            f.Saved.AuditLog.Should().NotContain("forged line").And.HaveCount(1);
            f.Saved.SeasonId.Should().Be("season-1");
            f.Saved.UserId.Should().Be(EndpointKit.PlayerId);
        }

        [Fact]
        public async Task CreateEntry_ShouldKeepStoredServerOwnedFields_WhenPlayerEditsOwnEntry()
        {
            var f = new Fixture();
            var stored = MakeEntry("e1", EndpointKit.PlayerId);
            stored.IsAdminEntered = true;
            stored.CreatedOn = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
            f.Stored(stored);
            var body = MakeEntry("e1", EndpointKit.PlayerId, pick: "Team B");
            body.IsAdminEntered = false;
            body.CreatedOn = DateTime.UtcNow;

            await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: body));

            f.Saved!.IsAdminEntered.Should().BeTrue();
            f.Saved.CreatedOn.Should().Be(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
            f.Saved.AuditLog.First().Should().Contain("Entry Created by owner@test.com");
            f.Saved.AuditLog.Should().Contain(l => l.Contains("Changed Winner: First Bowl - Team A -> Team B"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateEntry_ShouldRefuse_WhenBracketNameIsBlank(string name)
        {
            var f = new Fixture();
            var body = MakeEntry("e1", "");
            body.PlayerName = name;

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: body));

            EndpointKit.Status(result).Should().Be(400);
            f.Entries.Verify(e => e.AddEntryAsync(It.IsAny<BracketEntry>()), Times.Never);
        }

        [Fact]
        public async Task CreateEntry_ShouldRefuse_WhenBracketNameIsTooLong()
        {
            var f = new Fixture();
            var body = MakeEntry("e1", "");
            body.PlayerName = new string('x', 500);

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: body));

            EndpointKit.Status(result).Should().Be(400);
            f.Entries.Verify(e => e.AddEntryAsync(It.IsAny<BracketEntry>()), Times.Never);
        }

        [Fact]
        public async Task CreateEntry_ShouldTrimBracketName_WhenNameHasSpaces()
        {
            var f = new Fixture();
            var body = MakeEntry("e1", "");
            body.PlayerName = "  Padded  ";

            await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: body));

            f.Saved!.PlayerName.Should().Be("Padded");
        }

        [Fact]
        public async Task CreateEntry_ShouldRefuse_WhenPicksIncludeAGameOutsideThePool()
        {
            var f = new Fixture();
            var body = MakeEntry("e1", "");
            body.Picks!["not-a-game-here"] = "Team Z";

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: body));

            EndpointKit.Status(result).Should().Be(400);
            f.Entries.Verify(e => e.AddEntryAsync(It.IsAny<BracketEntry>()), Times.Never);
        }

        [Fact]
        public async Task CreateEntry_ShouldUseThePoolsGameList_WhenThePoolNamesItsGames()
        {
            var f = new Fixture();
            f.Pools.Setup(p => p.GetPoolAsync("pool-open")).ReturnsAsync(new BowlPool
            {
                Id = "pool-open", SeasonId = "season-1", Name = "open", InviteCode = "code",
                LockDate = DateTime.UtcNow.AddDays(7), GameIds = new List<string> { "game-2" }
            });
            var body = MakeEntry("e1", ""); // picks game-1, which is in the season but not in this pool

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: body));

            EndpointKit.Status(result).Should().Be(400);
        }

        [Fact]
        public async Task CreateEntry_ShouldStillSave_WhenStoredEntryAlreadyCarriesAStalePick()
        {
            var f = new Fixture();
            var stored = MakeEntry("e1", EndpointKit.PlayerId);
            stored.Picks!["old-removed-game"] = "Team Q";
            f.Stored(stored);
            var body = MakeEntry("e1", EndpointKit.PlayerId, pick: "Team B");
            body.Picks!["old-removed-game"] = "Team Q";

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: body));

            EndpointKit.Status(result).Should().Be(200, "an old stale pick must not lock the player out of saving");
        }

        [Fact]
        public async Task CreateEntry_ShouldRefuse_WhenAPickIsTooLong()
        {
            var f = new Fixture();
            var body = MakeEntry("e1", "", pick: new string('x', 500));

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: body));

            EndpointKit.Status(result).Should().Be(400);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(100000)]
        public async Task CreateEntry_ShouldRefuse_WhenTiebreakerIsOutOfRange(int points)
        {
            var f = new Fixture();
            var body = MakeEntry("e1", "");
            body.TieBreakerPoints = points;

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: body));

            EndpointKit.Status(result).Should().Be(400);
        }

        [Theory]
        [InlineData(Tier.Admin)]
        [InlineData(Tier.SuperAdmin)]
        public async Task CreateEntry_ShouldNotSetPaid_WhenAdminSavesAnEntryWithAPaidFlag(Tier tier)
        {
            var f = new Fixture();
            var body = MakeEntry("e1", EndpointKit.PlayerId);
            body.IsPaid = true;

            await f.Build().CreateEntry(EndpointKit.Request(tier, body: body));

            f.Saved!.IsPaid.Should().BeFalse("payment changes only through SetEntryPaidStatus");
        }

        // ── Release 2 group C: an admin-keyed entry names its owner ─────────────

        [Theory]
        [InlineData(Tier.Admin)]
        [InlineData(Tier.SuperAdmin)]
        public async Task CreateEntry_ShouldRefuse_WhenAdminGivesNoOwner(Tier tier)
        {
            var f = new Fixture();
            var body = MakeEntry("e1", "");

            var result = await f.Build().CreateEntry(EndpointKit.Request(tier, body: body));

            EndpointKit.Status(result).Should().Be(400);
            f.Saved.Should().BeNull();
        }

        [Fact]
        public async Task CreateEntry_ShouldSaveTheTypedOwnerWithNoAccount_WhenAdminTypesAName()
        {
            var f = new Fixture();
            var body = MakeEntry("e1", "");
            body.OwnerName = "Grandma Jo";

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Admin, body: body));

            EndpointKit.Status(result).Should().Be(200);
            f.Saved!.UserId.Should().BeEmpty();
            f.Saved.OwnerName.Should().Be("Grandma Jo");
            f.Saved.IsAdminEntered.Should().BeTrue();
        }

        [Fact]
        public async Task CreateEntry_ShouldIgnoreTheOwnerName_WhenPlayerSaves()
        {
            var f = new Fixture();
            var body = MakeEntry("e1", "");
            body.OwnerName = "Someone";

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: body));

            EndpointKit.Status(result).Should().Be(200);
            f.Saved!.UserId.Should().Be(EndpointKit.PlayerId);
            f.Saved.OwnerName.Should().BeNull();
        }

        // ── #131 ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task CreateEntry_ShouldRefuseMove_WhenPlayerMovesEntryBetweenTwoOpenPools()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId, "pool-open"));

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: MakeEntry("e1", EndpointKit.PlayerId, "pool-open-2")));

            EndpointKit.Status(result).Should().Be(400);
            f.Entries.Verify(e => e.AddEntryAsync(It.IsAny<BracketEntry>()), Times.Never);
        }

        [Theory]
        [InlineData(Tier.Admin)]
        [InlineData(Tier.SuperAdmin)]
        public async Task CreateEntry_ShouldMoveEntry_WhenAdminSendsAnotherPool(Tier tier)
        {
            // Admins move entries on purpose (the dedicated Move Entry action arrives with #132).
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId, "pool-open"));

            var result = await f.Build().CreateEntry(EndpointKit.Request(tier, body: MakeEntry("e1", EndpointKit.PlayerId, "pool-open-2")));

            EndpointKit.Status(result).Should().Be(200);
            f.Saved!.PoolId.Should().Be("pool-open-2");
        }

        // ── #97: what the read endpoints return ───────────────────────────────

        [Theory]
        [InlineData(Tier.Anonymous)]
        [InlineData(Tier.Player)]
        public async Task GetEntries_ShouldReturn400_WhenNonAdminLeavesOutSeasonOrPool(Tier tier)
        {
            var f = new Fixture();
            f.Listed(MakeEntry("e1", EndpointKit.OtherPlayerId));

            foreach (var query in new[] { "", "?seasonId=season-1", "?poolId=pool-open" })
            {
                var result = await f.Build().GetEntries(EndpointKit.Request(tier, query));
                EndpointKit.Status(result).Should().Be(400, query);
            }

            f.Entries.Verify(e => e.GetEntriesAsync(It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
        }

        [Theory]
        [InlineData(Tier.Admin)]
        [InlineData(Tier.SuperAdmin)]
        public async Task GetEntries_ShouldListEverythingWithPicks_WhenAdminLeavesOutSeasonAndPool(Tier tier)
        {
            var f = new Fixture();
            f.Listed(MakeEntry("e1", EndpointKit.OtherPlayerId, "pool-open", SecretPick));

            var result = await f.Build().GetEntries(EndpointKit.Request(tier));

            EndpointKit.Status(result).Should().Be(200);
            var body = EndpointKit.BodyText(result);
            body.Should().Contain(SecretPick).And.Contain(EndpointKit.OtherPlayerId).And.Contain("owner@test.com");
        }

        [Fact]
        public async Task GetEntries_ShouldHidePicksUserIdAuditLogAndTiebreaker_WhenPlayerReadsAnotherPlayersEntryBeforeLock()
        {
            var f = new Fixture();
            f.Listed(MakeEntry("e1", EndpointKit.OtherPlayerId, "pool-open", SecretPick));

            var result = await f.Build().GetEntries(EndpointKit.Request(Tier.Player, "?seasonId=season-1&poolId=pool-open"));

            var body = EndpointKit.BodyText(result);
            body.Should().NotContain(SecretPick).And.NotContain(EndpointKit.OtherPlayerId).And.NotContain("owner@test.com");
            using var json = JsonDocument.Parse(body);
            var entry = json.RootElement[0];
            entry.GetProperty("playerName").GetString().Should().Be("Bracket e1");
            entry.GetProperty("picks").ValueKind.Should().Be(JsonValueKind.Null);
            entry.GetProperty("tieBreakerPoints").GetInt32().Should().Be(0);
            entry.GetProperty("auditLog").GetArrayLength().Should().Be(0);
        }

        [Fact]
        public async Task GetEntries_ShouldReturnOwnEntryInFull_WhenPlayerReadsItBeforeLock()
        {
            var f = new Fixture();
            f.Listed(MakeEntry("e1", EndpointKit.PlayerId, "pool-open", SecretPick));

            var result = await f.Build().GetEntries(EndpointKit.Request(Tier.Player, "?seasonId=season-1&poolId=pool-open"));

            EndpointKit.BodyText(result).Should().Contain(SecretPick);
        }

        [Theory]
        [InlineData(Tier.Anonymous)]
        [InlineData(Tier.Player)]
        public async Task GetEntries_ShouldShowPicksButNotUserIdOrAuditLog_WhenPoolIsLocked(Tier tier)
        {
            var f = new Fixture();
            f.Listed(MakeEntry("e1", EndpointKit.OtherPlayerId, "pool-locked", SecretPick));

            var result = await f.Build().GetEntries(EndpointKit.Request(tier, "?seasonId=season-1&poolId=pool-locked"));

            var body = EndpointKit.BodyText(result);
            body.Should().Contain(SecretPick).And.NotContain(EndpointKit.OtherPlayerId).And.NotContain("owner@test.com");
        }

        [Fact]
        public async Task GetEntries_ShouldJudgeEachEntryByItsOwnPool_WhenTheListSpansPools()
        {
            var f = new Fixture();
            f.Listed(MakeEntry("open-one", EndpointKit.OtherPlayerId, "pool-open", "OpenPoolPick"),
                     MakeEntry("locked-one", EndpointKit.OtherPlayerId, "pool-locked", "LockedPoolPick"));

            var result = await f.Build().GetEntries(EndpointKit.Request(Tier.Player, "?seasonId=season-1&poolId=pool-open"));

            var body = EndpointKit.BodyText(result);
            body.Should().NotContain("OpenPoolPick").And.Contain("LockedPoolPick");
        }

        [Fact]
        public async Task GetEntry_ShouldHideUserIdAndAuditLog_WhenPlayerReadsAnotherPlayersEntryAfterLock()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.OtherPlayerId, "pool-locked", SecretPick));

            var result = await f.Build().GetEntry(EndpointKit.Request(Tier.Player, "?id=e1&seasonId=season-1"));

            var body = EndpointKit.BodyText(result);
            body.Should().Contain(SecretPick).And.NotContain(EndpointKit.OtherPlayerId).And.NotContain("owner@test.com");
        }

        [Fact]
        public async Task GetEntry_ShouldReturnEverything_WhenAdminReadsAnEntryBeforeLock()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.OtherPlayerId, "pool-open", SecretPick));

            var result = await f.Build().GetEntry(EndpointKit.Request(Tier.Admin, "?id=e1&seasonId=season-1"));

            EndpointKit.BodyText(result).Should().Contain(SecretPick).And.Contain(EndpointKit.OtherPlayerId).And.Contain("owner@test.com");
        }

        // ── Release 2 group C: entries with no account (an owner name only) ─────

        private static BracketEntry NoAccountEntry(string poolId) 
        {
            var entry = MakeEntry("e1", "", poolId, SecretPick);
            entry.OwnerName = "Grandma Jo";
            return entry;
        }

        [Fact]
        public async Task CreateEntry_ShouldReturn403_WhenPlayerEditsAnEntryWithNoAccount()
        {
            var f = new Fixture();
            f.Stored(NoAccountEntry("pool-open"));
            var body = MakeEntry("e1", "", "pool-open", "Team B");

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: body));

            EndpointKit.Status(result).Should().Be(403);
            f.Saved.Should().BeNull();
        }

        [Theory]
        [InlineData("pool-open")]
        [InlineData("pool-locked")]
        public async Task GetEntry_ShouldHideTheOwnerName_WhenPlayerReadsAnEntryWithNoAccount(string poolId)
        {
            var f = new Fixture();
            f.Stored(NoAccountEntry(poolId));

            var result = await f.Build().GetEntry(EndpointKit.Request(Tier.Player, "?id=e1&seasonId=season-1"));

            EndpointKit.BodyText(result).Should().NotContain("Grandma Jo");
        }

        [Fact]
        public async Task GetEntry_ShouldHidePicks_WhenPlayerReadsAnEntryWithNoAccountBeforeLock()
        {
            var f = new Fixture();
            f.Stored(NoAccountEntry("pool-open"));

            var result = await f.Build().GetEntry(EndpointKit.Request(Tier.Player, "?id=e1&seasonId=season-1"));

            EndpointKit.BodyText(result).Should().NotContain(SecretPick);
        }

        [Fact]
        public async Task GetEntries_ShouldHideTheOwnerName_WhenPlayerListsAPoolWithAnEntryWithNoAccount()
        {
            var f = new Fixture();
            f.Listed(NoAccountEntry("pool-open"));

            var result = await f.Build().GetEntries(EndpointKit.Request(Tier.Player, "?seasonId=season-1&poolId=pool-open"));

            EndpointKit.BodyText(result).Should().NotContain("Grandma Jo");
        }
    }

    /// <summary>Group B entry rules for hoops: the same rules as football, on the hoops functions.</summary>
    public class HoopsEntryRulesTests
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
                Pools.Setup(p => p.GetPoolAsync("pool-open-2")).ReturnsAsync(MakePool("pool-open-2", locked: false));
                Pools.Setup(p => p.GetPoolAsync("pool-locked")).ReturnsAsync(MakePool("pool-locked", locked: true));
                Games.Setup(g => g.GetGamesAsync(It.IsAny<string>())).ReturnsAsync(new List<HoopsGame> { new() { Id = "game-1" } });
                Entries.Setup(e => e.IsBracketNameTakenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(false);
                Entries.Setup(e => e.AddEntryAsync(It.IsAny<BracketEntry>()))
                    .Callback<BracketEntry>(e => Saved = e)
                    .Returns(Task.CompletedTask);
            }

            public void Stored(BracketEntry entry) =>
                Entries.Setup(e => e.GetEntryAsync(entry.Id, entry.SeasonId)).ReturnsAsync(entry);

            public void Listed(params BracketEntry[] entries) =>
                Entries.Setup(e => e.GetEntriesAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(entries.ToList());

            public HoopsEntryFunctions Build() => new(new NullLoggerFactory(), Entries.Object, Pools.Object, Games.Object, Users.Object);
        }

        private static HoopsPool MakePool(string id, bool locked) => new()
        {
            Id = id, SeasonId = "season-1", Name = id, InviteCode = "code",
            LockDate = locked ? DateTime.UtcNow.AddDays(-1) : DateTime.UtcNow.AddDays(7)
        };

        private static BracketEntry MakeEntry(string id, string userId, string poolId = "pool-open", string pick = "Team A") => new()
        {
            Id = id, SeasonId = "season-1", PoolId = poolId, UserId = userId,
            PlayerName = "Bracket " + id,
            Picks = new Dictionary<string, string> { ["game-1"] = pick },
            TieBreakerPoints = 140,
            Type = Constants.DocumentTypes.HoopsBracketEntry,
            AuditLog = new List<string> { "Entry Created by owner@test.com on Dec 1" }
        };

        [Fact]
        public async Task CreateHoopsEntry_ShouldIgnoreServerOwnedFields_WhenPlayerSendsThem()
        {
            var f = new Fixture();
            var body = MakeEntry("e1", EndpointKit.OtherPlayerId);
            body.IsAdminEntered = true;
            body.IsPaid = true;
            body.CreatedOn = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            body.Type = "Something Else";
            body.AuditLog = new List<string> { "forged line" };

            var result = await f.Build().CreateHoopsEntry(EndpointKit.Request(Tier.Player, body: body));

            EndpointKit.Status(result).Should().Be(200);
            f.Saved!.IsAdminEntered.Should().BeFalse();
            f.Saved.IsPaid.Should().BeFalse();
            f.Saved.CreatedOn.Should().BeAfter(new DateTime(2020, 1, 1));
            f.Saved.Type.Should().Be(Constants.DocumentTypes.HoopsBracketEntry);
            f.Saved.AuditLog.Should().NotContain("forged line");
            f.Saved.UserId.Should().Be(EndpointKit.PlayerId);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateHoopsEntry_ShouldRefuse_WhenBracketNameIsBlank(string name)
        {
            var f = new Fixture();
            var body = MakeEntry("e1", "");
            body.PlayerName = name;

            var result = await f.Build().CreateHoopsEntry(EndpointKit.Request(Tier.Player, body: body));

            EndpointKit.Status(result).Should().Be(400);
            f.Entries.Verify(e => e.AddEntryAsync(It.IsAny<BracketEntry>()), Times.Never);
        }

        [Fact]
        public async Task CreateHoopsEntry_ShouldRefuse_WhenBracketNameIsTooLong()
        {
            var f = new Fixture();
            var body = MakeEntry("e1", "");
            body.PlayerName = new string('x', 500);

            var result = await f.Build().CreateHoopsEntry(EndpointKit.Request(Tier.Player, body: body));

            EndpointKit.Status(result).Should().Be(400);
        }

        [Fact]
        public async Task CreateHoopsEntry_ShouldRefuse_WhenPicksIncludeAGameOutsideThePool()
        {
            var f = new Fixture();
            var body = MakeEntry("e1", "");
            body.Picks!["not-a-game-here"] = "Team Z";

            var result = await f.Build().CreateHoopsEntry(EndpointKit.Request(Tier.Player, body: body));

            EndpointKit.Status(result).Should().Be(400);
            f.Entries.Verify(e => e.AddEntryAsync(It.IsAny<BracketEntry>()), Times.Never);
        }

        [Fact]
        public async Task CreateHoopsEntry_ShouldRefuseMove_WhenPlayerMovesEntryBetweenTwoOpenPools()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId, "pool-open"));

            var result = await f.Build().CreateHoopsEntry(EndpointKit.Request(Tier.Player, body: MakeEntry("e1", EndpointKit.PlayerId, "pool-open-2")));

            EndpointKit.Status(result).Should().Be(400);
            f.Entries.Verify(e => e.AddEntryAsync(It.IsAny<BracketEntry>()), Times.Never);
        }

        [Theory]
        [InlineData(Tier.Admin)]
        [InlineData(Tier.SuperAdmin)]
        public async Task CreateHoopsEntry_ShouldMoveEntry_WhenAdminSendsAnotherPool(Tier tier)
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId, "pool-open"));

            var result = await f.Build().CreateHoopsEntry(EndpointKit.Request(tier, body: MakeEntry("e1", EndpointKit.PlayerId, "pool-open-2")));

            EndpointKit.Status(result).Should().Be(200);
            f.Saved!.PoolId.Should().Be("pool-open-2");
        }

        [Theory]
        [InlineData(Tier.Anonymous)]
        [InlineData(Tier.Player)]
        public async Task GetHoopsEntries_ShouldReturn400_WhenNonAdminLeavesOutSeasonOrPool(Tier tier)
        {
            var f = new Fixture();
            f.Listed(MakeEntry("e1", EndpointKit.OtherPlayerId));

            foreach (var query in new[] { "", "?seasonId=season-1", "?poolId=pool-open" })
            {
                var result = await f.Build().GetHoopsEntries(EndpointKit.Request(tier, query));
                EndpointKit.Status(result).Should().Be(400, query);
            }
        }

        [Fact]
        public async Task GetHoopsEntries_ShouldListEverythingWithPicks_WhenAdminLeavesOutSeasonAndPool()
        {
            var f = new Fixture();
            f.Listed(MakeEntry("e1", EndpointKit.OtherPlayerId, "pool-open", SecretPick));

            var result = await f.Build().GetHoopsEntries(EndpointKit.Request(Tier.Admin));

            EndpointKit.Status(result).Should().Be(200);
            EndpointKit.BodyText(result).Should().Contain(SecretPick).And.Contain("owner@test.com");
        }

        [Fact]
        public async Task GetHoopsEntries_ShouldHidePicksUserIdAuditLogAndTiebreaker_WhenPlayerReadsAnotherPlayersEntryBeforeLock()
        {
            var f = new Fixture();
            f.Listed(MakeEntry("e1", EndpointKit.OtherPlayerId, "pool-open", SecretPick));

            var result = await f.Build().GetHoopsEntries(EndpointKit.Request(Tier.Player, "?seasonId=season-1&poolId=pool-open"));

            var body = EndpointKit.BodyText(result);
            body.Should().NotContain(SecretPick).And.NotContain(EndpointKit.OtherPlayerId).And.NotContain("owner@test.com");
            using var json = JsonDocument.Parse(body);
            json.RootElement[0].GetProperty("tieBreakerPoints").GetInt32().Should().Be(0);
        }

        [Fact]
        public async Task GetHoopsEntries_ShouldShowPicksButNotUserIdOrAuditLog_WhenPoolIsLocked()
        {
            var f = new Fixture();
            f.Listed(MakeEntry("e1", EndpointKit.OtherPlayerId, "pool-locked", SecretPick));

            var result = await f.Build().GetHoopsEntries(EndpointKit.Request(Tier.Anonymous, "?seasonId=season-1&poolId=pool-locked"));

            var body = EndpointKit.BodyText(result);
            body.Should().Contain(SecretPick).And.NotContain(EndpointKit.OtherPlayerId).And.NotContain("owner@test.com");
        }

        [Fact]
        public async Task GetHoopsEntry_ShouldReturnOwnEntryInFull_WhenPlayerReadsItBeforeLock()
        {
            var f = new Fixture();
            f.Stored(MakeEntry("e1", EndpointKit.PlayerId, "pool-open", SecretPick));

            var result = await f.Build().GetHoopsEntry(EndpointKit.Request(Tier.Player, "?id=e1&seasonId=season-1"));

            EndpointKit.BodyText(result).Should().Contain(SecretPick);
        }
    }
}
