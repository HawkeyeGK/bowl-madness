using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BowlPoolManager.Api.Functions;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// Group D entry-window rules for football: Move Entry (#132), picks that cannot be made (#128) and
    /// the admin-entered flag (#161). BUSINESS-RULES section 3.
    /// </summary>
    public class GroupDEntryWindowTests
    {
        private class Fixture
        {
            public Mock<IEntryRepository> Entries = new();
            public Mock<IPoolRepository> Pools = new();
            public Mock<IGameRepository> Games = new();
            public Mock<IUserRepository> Users = EndpointKit.Users();
            public BracketEntry? Saved;

            public Fixture()
            {
                Pools.Setup(p => p.GetPoolAsync("pool-a")).ReturnsAsync(Pool("pool-a", "Family"));
                Pools.Setup(p => p.GetPoolAsync("pool-b")).ReturnsAsync(Pool("pool-b", "Office"));
                Pools.Setup(p => p.GetPoolAsync("pool-archived")).ReturnsAsync(Pool("pool-archived", "Old", archived: true));
                Pools.Setup(p => p.GetPoolAsync("pool-last-year")).ReturnsAsync(Pool("pool-last-year", "Family 2025", season: "season-0"));
                Games.Setup(g => g.GetGamesAsync(It.IsAny<string?>())).ReturnsAsync(Bracket());
                Entries.Setup(e => e.IsBracketNameTakenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(false);
                Entries.Setup(e => e.AddEntryAsync(It.IsAny<BracketEntry>()))
                    .Callback<BracketEntry>(e => Saved = e)
                    .Returns(Task.CompletedTask);
            }

            public void Stored(BracketEntry entry) =>
                Entries.Setup(e => e.GetEntryAsync(entry.Id, entry.SeasonId)).ReturnsAsync(entry);

            public EntryFunctions Build() => new(new NullLoggerFactory(), Entries.Object, Pools.Object, Games.Object, Users.Object);
        }

        private static BowlPool Pool(string id, string name, bool archived = false, string season = "season-1") => new()
        {
            Id = id, SeasonId = season, Name = name, InviteCode = "code", IsArchived = archived, LockDate = DateTime.UtcNow.AddDays(7)
        };

        private static List<BowlGame> Bracket() => new()
        {
            new() { Id = "r1a", BowlName = "R1A", TeamHome = "Miami", TeamAway = "Texas A&M", NextGameId = "qf", IsPlayoff = true, StartTime = new DateTime(2026, 12, 20, 12, 0, 0, DateTimeKind.Utc) },
            new() { Id = "r1b", BowlName = "R1B", TeamHome = "Oregon", TeamAway = "James Madison", NextGameId = "qf", IsPlayoff = true, StartTime = new DateTime(2026, 12, 20, 13, 0, 0, DateTimeKind.Utc) },
            new() { Id = "qf", BowlName = "QF", TeamHome = "Winner of R1A", TeamAway = "Winner of R1B", IsPlayoff = true, StartTime = new DateTime(2026, 12, 20, 18, 0, 0, DateTimeKind.Utc) }
        };

        private static BracketEntry Entry(string id, string userId, string poolId, params (string game, string team)[] picks) => new()
        {
            Id = id, SeasonId = "season-1", PoolId = poolId, UserId = userId, PlayerName = "Bracket " + id,
            Picks = picks.ToDictionary(p => p.game, p => p.team),
            AuditLog = new List<string> { "Entry Created by player1@test.com on Dec 1" }
        };

        private static string Query(string entryId = "e1", string seasonId = "season-1", string newPoolId = "pool-b") =>
            $"?entryId={entryId}&seasonId={seasonId}&newPoolId={newPoolId}";

        // ── #132: MoveEntry ─────────────────────────────────────────────────────

        [Theory]
        [InlineData(Tier.Admin)]
        [InlineData(Tier.SuperAdmin)]
        public async Task MoveEntry_ShouldMoveTheEntryAndLogIt_WhenAdminMovesToAnotherPoolOfTheSameSeason(Tier tier)
        {
            var f = new Fixture();
            f.Stored(Entry("e1", EndpointKit.PlayerId, "pool-a", ("r1a", "Miami")));

            var result = await f.Build().MoveEntry(EndpointKit.Request(tier, Query()));

            EndpointKit.Status(result).Should().Be(200);
            f.Saved!.PoolId.Should().Be("pool-b");
            f.Saved.UserId.Should().Be(EndpointKit.PlayerId);
            f.Saved.Picks.Should().ContainKey("r1a");
            f.Saved.AuditLog.Should().HaveCount(2);
            f.Saved.AuditLog.Last().Should().Contain("Moved by").And.Contain("Family -> Office");
        }

        [Theory]
        [InlineData(Tier.Anonymous, 401)]
        [InlineData(Tier.Player, 403)]
        public async Task MoveEntry_ShouldRefuse_WhenCallerIsNotAnAdmin(Tier tier, int status)
        {
            var f = new Fixture();
            f.Stored(Entry("e1", EndpointKit.PlayerId, "pool-a")); // even the entry's own owner

            var result = await f.Build().MoveEntry(EndpointKit.Request(tier, Query()));

            EndpointKit.Status(result).Should().Be(status);
            f.Entries.Verify(e => e.AddEntryAsync(It.IsAny<BracketEntry>()), Times.Never);
            f.Entries.Verify(e => e.GetEntryAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never, "the caller is checked before anything is read");
        }

        [Fact]
        public async Task MoveEntry_ShouldRefuse_WhenTheNameIsTakenInTheTargetPool()
        {
            var f = new Fixture();
            f.Stored(Entry("e1", EndpointKit.PlayerId, "pool-a"));
            f.Entries.Setup(e => e.IsBracketNameTakenAsync("pool-b", "Bracket e1", "e1")).ReturnsAsync(true);

            var result = await f.Build().MoveEntry(EndpointKit.Request(Tier.Admin, Query()));

            EndpointKit.Status(result).Should().Be(409);
            EndpointKit.BodyText(result).Should().Contain("Bracket e1").And.Contain("Office");
            f.Entries.Verify(e => e.AddEntryAsync(It.IsAny<BracketEntry>()), Times.Never);
        }

        [Fact]
        public async Task MoveEntry_ShouldRefuse_WhenTheTargetPoolIsInAnotherSeason()
        {
            var f = new Fixture();
            f.Stored(Entry("e1", EndpointKit.PlayerId, "pool-a"));

            var result = await f.Build().MoveEntry(EndpointKit.Request(Tier.Admin, Query(newPoolId: "pool-last-year")));

            EndpointKit.Status(result).Should().Be(400);
            f.Entries.Verify(e => e.AddEntryAsync(It.IsAny<BracketEntry>()), Times.Never);
        }

        [Fact]
        public async Task MoveEntry_ShouldRefuse_WhenTheTargetPoolIsArchived()
        {
            var f = new Fixture();
            f.Stored(Entry("e1", EndpointKit.PlayerId, "pool-a"));

            var result = await f.Build().MoveEntry(EndpointKit.Request(Tier.Admin, Query(newPoolId: "pool-archived")));

            EndpointKit.Status(result).Should().Be(400);
            f.Entries.Verify(e => e.AddEntryAsync(It.IsAny<BracketEntry>()), Times.Never);
        }

        [Fact]
        public async Task MoveEntry_ShouldRefuse_WhenTheEntryIsAlreadyInThatPool()
        {
            var f = new Fixture();
            f.Stored(Entry("e1", EndpointKit.PlayerId, "pool-b"));

            var result = await f.Build().MoveEntry(EndpointKit.Request(Tier.Admin, Query()));

            EndpointKit.Status(result).Should().Be(400);
        }

        [Theory]
        [InlineData("?seasonId=season-1&newPoolId=pool-b")]
        [InlineData("?entryId=e1&newPoolId=pool-b")]
        [InlineData("?entryId=e1&seasonId=season-1")]
        public async Task MoveEntry_ShouldReturn400_WhenAParameterIsMissing(string query)
        {
            var f = new Fixture();

            var result = await f.Build().MoveEntry(EndpointKit.Request(Tier.Admin, query));

            EndpointKit.Status(result).Should().Be(400);
        }

        [Fact]
        public async Task MoveEntry_ShouldReturn404_WhenTheDocumentIsNotAFootballEntry()
        {
            var f = new Fixture();
            var other = Entry("e1", EndpointKit.PlayerId, "pool-a");
            other.Type = "SomethingElse";
            f.Stored(other);

            var result = await f.Build().MoveEntry(EndpointKit.Request(Tier.Admin, Query()));

            EndpointKit.Status(result).Should().Be(404);
            f.Entries.Verify(e => e.AddEntryAsync(It.IsAny<BracketEntry>()), Times.Never);
        }

        [Fact]
        public async Task MoveEntry_ShouldReturn404_WhenTheEntryOrThePoolDoesNotExist()
        {
            var f = new Fixture();
            f.Stored(Entry("e1", EndpointKit.PlayerId, "pool-a"));

            EndpointKit.Status(await f.Build().MoveEntry(EndpointKit.Request(Tier.Admin, Query(entryId: "nope")))).Should().Be(404);
            EndpointKit.Status(await f.Build().MoveEntry(EndpointKit.Request(Tier.Admin, Query(newPoolId: "nope")))).Should().Be(404);
        }

        // ── #128: CreateEntry rejects picks that cannot be made ─────────────────

        [Fact]
        public async Task CreateEntry_ShouldRefuse_WhenAPickIsPlaceholderText()
        {
            var f = new Fixture();
            var body = Entry("", EndpointKit.PlayerId, "pool-a", ("r1a", "Miami"), ("qf", "Winner of R1B"));

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: body));

            EndpointKit.Status(result).Should().Be(400);
            f.Entries.Verify(e => e.AddEntryAsync(It.IsAny<BracketEntry>()), Times.Never);
        }

        [Fact]
        public async Task CreateEntry_ShouldRefuse_WhenALaterPickIsATeamTheEarlierPicksDoNotReach()
        {
            var f = new Fixture();
            var body = Entry("", EndpointKit.PlayerId, "pool-a", ("r1a", "Texas A&M"), ("r1b", "Oregon"), ("qf", "Miami"));

            var result = await f.Build().CreateEntry(EndpointKit.Request(Tier.Player, body: body));

            EndpointKit.Status(result).Should().Be(400);
        }

        [Theory]
        [InlineData(Tier.Player)]
        [InlineData(Tier.Admin)]
        public async Task CreateEntry_ShouldSave_WhenEveryPickIsReachable(Tier tier)
        {
            var f = new Fixture();
            var body = Entry("", EndpointKit.PlayerId, "pool-a", ("r1a", "Texas A&M"), ("r1b", "Oregon"), ("qf", "Texas A&M"));

            var result = await f.Build().CreateEntry(EndpointKit.Request(tier, body: body));

            EndpointKit.Status(result).Should().Be(200);
        }

        // ── #161: IsAdminEntered ────────────────────────────────────────────────

        [Fact]
        public async Task CreateEntry_ShouldSetIsAdminEntered_WhenAnAdminCreatesAnEntryForAPlayer()
        {
            var f = new Fixture();

            await f.Build().CreateEntry(EndpointKit.Request(Tier.Admin, body: Entry("", EndpointKit.PlayerId, "pool-a", ("r1a", "Miami"))));

            f.Saved!.UserId.Should().Be(EndpointKit.PlayerId);
            f.Saved.IsAdminEntered.Should().BeTrue();
        }

        [Fact]
        public async Task CreateEntry_ShouldNotSetIsAdminEntered_WhenAnAdminCreatesTheirOwnEntry()
        {
            var f = new Fixture();

            await f.Build().CreateEntry(EndpointKit.Request(Tier.Admin, "?assignToMe=true", Entry("", EndpointKit.PlayerId, "pool-a", ("r1a", "Miami"))));

            f.Saved!.UserId.Should().Be(EndpointKit.AdminId);
            f.Saved.IsAdminEntered.Should().BeFalse();
        }
    }
}
