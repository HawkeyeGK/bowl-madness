using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BowlPoolManager.Api.Functions;
using BowlPoolManager.Api.Functions.Admin;
using BowlPoolManager.Api.Services.ScoreFeed;
using BowlPoolManager.Api.Helpers;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// BUSINESS-RULES section 1: a disabled user is treated exactly like an unauthenticated user, on
    /// every endpoint. Today the server never reads IsDisabled (#98). One test goes through the shared
    /// SuperAdmin check and one through an entry endpoint that reads the sign-in header directly.
    /// </summary>
    public class EndpointAuthDisabledAccountTests
    {
        // #98. A disabled SuperAdmin must be refused like an anonymous caller (401).
        [Fact]
        public async Task ValidateSuperAdminAsync_ShouldReturn401_WhenSuperAdminIsDisabled()
        {
            var req = EndpointKit.Request(EndpointKit.DisabledSuperAdminId);

            var result = await SecurityHelper.ValidateSuperAdminAsync(req, EndpointKit.Users().Object);

            result.IsValid.Should().BeFalse("a disabled account is treated as not signed in");
            result.ErrorResponse.Should().NotBeNull();
            EndpointKit.Status(result.ErrorResponse!).Should().Be(401);
        }

        // #98. A disabled player must not delete their own entry.
        [Fact]
        public async Task DeleteEntry_ShouldReturn401AndDeleteNothing_WhenCallerIsDisabled()
        {
            var entries = new Mock<IEntryRepository>();
            entries.Setup(e => e.GetEntryAsync("e1", "season-1")).ReturnsAsync(new BracketEntry
            {
                Id = "e1", SeasonId = "season-1", PoolId = "pool-1", UserId = EndpointKit.DisabledPlayerId
            });
            var pools = new Mock<IPoolRepository>();
            pools.Setup(p => p.GetPoolAsync(It.IsAny<string>())).ReturnsAsync(new BowlPool
            {
                Id = "pool-1", SeasonId = "season-1", LockDate = DateTime.UtcNow.AddDays(7)
            });
            var sut = new EntryFunctions(new NullLoggerFactory(), entries.Object, pools.Object,
                new Mock<IGameRepository>().Object, EndpointKit.Users().Object);

            var result = await sut.DeleteEntry(EndpointKit.Request(EndpointKit.DisabledPlayerId, "?seasonId=season-1"), "e1");

            EndpointKit.Status(result).Should().Be(401);
            entries.Verify(e => e.DeleteEntryAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        // Guard for the fix: an enabled account must keep working through the same shared check.
        [Fact]
        public async Task ValidateSuperAdminAsync_ShouldBeValid_WhenSuperAdminIsEnabled()
        {
            var req = EndpointKit.Request(EndpointKit.SuperAdminId);

            var result = await SecurityHelper.ValidateSuperAdminAsync(req, EndpointKit.Users().Object);

            result.IsValid.Should().BeTrue();
        }

        // ── #98: every endpoint that reads the sign-in header refuses a disabled account ─────────

        private static Mock<IMockFeedStore> NewMockFeedStore() => new();

        [Fact]
        public async Task GetCallerAsync_ShouldReturnNull_WhenAccountIsDisabled()
        {
            var caller = await SecurityHelper.GetCallerAsync(EndpointKit.Request(EndpointKit.DisabledPlayerId), EndpointKit.Users().Object);

            caller.Should().BeNull();
        }

        [Fact]
        public async Task GetCallerAsync_ShouldReturnNull_WhenAnAdminAccountIsDisabled()
        {
            var users = EndpointKit.Users();
            users.Object.GetUserAsync(EndpointKit.AdminId).Result!.IsDisabled = true;

            var caller = await SecurityHelper.GetCallerAsync(EndpointKit.Request(EndpointKit.AdminId), users.Object);

            caller.Should().BeNull("a disabled admin is treated as signed out");
        }

        [Fact]
        public async Task GetCallerAsync_ShouldReturnCallerWithNoProfile_WhenSignedInUserHasNoProfileYet()
        {
            var caller = await SecurityHelper.GetCallerAsync(EndpointKit.Request("brand-new-user"), EndpointKit.Users().Object);

            caller.Should().NotBeNull();
            caller!.Profile.Should().BeNull();
            caller.IsAdmin.Should().BeFalse();
        }

        [Fact]
        public async Task GetCallerAsync_ShouldReturnNull_WhenHeaderIsMissing()
        {
            var caller = await SecurityHelper.GetCallerAsync(EndpointKit.Request(Tier.Anonymous), EndpointKit.Users().Object);

            caller.Should().BeNull();
        }

        [Fact]
        public async Task RefuseUnlessSuperAdminAsync_ShouldReturn401_WhenHeaderIsMalformed()
        {
            var http = EndpointKit.AspRequest(Tier.Anonymous);
            http.Headers["x-ms-client-principal"] = "not base64 at all!";

            var refused = await SecurityHelper.RefuseUnlessSuperAdminAsync(http, EndpointKit.Users().Object);

            refused.Should().NotBeNull();
            EndpointKit.StatusOf(refused!).Should().Be(401);
        }

        [Fact]
        public async Task GetMyEntries_ShouldReturn401_WhenCallerIsDisabled()
        {
            var entries = new Mock<IEntryRepository>();
            var sut = new EntryFunctions(new NullLoggerFactory(), entries.Object, new Mock<IPoolRepository>().Object,
                new Mock<IGameRepository>().Object, EndpointKit.Users().Object);

            var result = await sut.GetMyEntries(EndpointKit.Request(EndpointKit.DisabledPlayerId, "?seasonId=season-1"));

            EndpointKit.Status(result).Should().Be(401);
            entries.Verify(e => e.GetEntriesForUserAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task CreateEntry_ShouldReturn401AndSaveNothing_WhenCallerIsDisabled()
        {
            var entries = new Mock<IEntryRepository>();
            var pools = new Mock<IPoolRepository>();
            var sut = new EntryFunctions(new NullLoggerFactory(), entries.Object, pools.Object,
                new Mock<IGameRepository>().Object, EndpointKit.Users().Object);
            var body = new BracketEntry { Id = "e1", PoolId = "pool-1", PlayerName = "Name" };

            var result = await sut.CreateEntry(EndpointKit.Request(EndpointKit.DisabledPlayerId, body: body));

            EndpointKit.Status(result).Should().Be(401);
            entries.Verify(e => e.AddEntryAsync(It.IsAny<BracketEntry>()), Times.Never);
        }

        [Fact]
        public async Task SetEntryPaidStatus_ShouldReturn401_WhenCallerIsADisabledAdmin()
        {
            var users = EndpointKit.Users();
            users.Object.GetUserAsync(EndpointKit.AdminId).Result!.IsDisabled = true;
            var entries = new Mock<IEntryRepository>();
            var sut = new EntryFunctions(new NullLoggerFactory(), entries.Object, new Mock<IPoolRepository>().Object,
                new Mock<IGameRepository>().Object, users.Object);

            var result = await sut.SetEntryPaidStatus(EndpointKit.Request(EndpointKit.AdminId, "?id=e1&seasonId=season-1&isPaid=true"));

            EndpointKit.Status(result).Should().Be(401);
            entries.Verify(e => e.AddEntryAsync(It.IsAny<BracketEntry>()), Times.Never);
        }

        [Fact]
        public async Task CreateHoopsEntry_ShouldReturn401AndSaveNothing_WhenCallerIsDisabled()
        {
            var entries = new Mock<IHoopsEntryRepository>();
            var sut = new HoopsEntryFunctions(new NullLoggerFactory(), entries.Object, new Mock<IHoopsPoolRepository>().Object,
                new Mock<IHoopsGameRepository>().Object, EndpointKit.Users().Object);
            var body = new BracketEntry { Id = "e1", PoolId = "pool-1", PlayerName = "Name" };

            var result = await sut.CreateHoopsEntry(EndpointKit.Request(EndpointKit.DisabledPlayerId, body: body));

            EndpointKit.Status(result).Should().Be(401);
            entries.Verify(e => e.AddEntryAsync(It.IsAny<BracketEntry>()), Times.Never);
        }

        [Fact]
        public async Task GetMyHoopsEntries_ShouldReturn401_WhenCallerIsDisabled()
        {
            var sut = new HoopsEntryFunctions(new NullLoggerFactory(), new Mock<IHoopsEntryRepository>().Object, new Mock<IHoopsPoolRepository>().Object,
                new Mock<IHoopsGameRepository>().Object, EndpointKit.Users().Object);

            var result = await sut.GetMyHoopsEntries(EndpointKit.Request(EndpointKit.DisabledPlayerId));

            EndpointKit.Status(result).Should().Be(401);
        }

        [Fact]
        public async Task GetEntries_ShouldTreatADisabledAdminAsAnonymous_WhenNoFiltersAreGiven()
        {
            var users = EndpointKit.Users();
            users.Object.GetUserAsync(EndpointKit.AdminId).Result!.IsDisabled = true;
            var sut = new EntryFunctions(new NullLoggerFactory(), new Mock<IEntryRepository>().Object, new Mock<IPoolRepository>().Object,
                new Mock<IGameRepository>().Object, users.Object);

            var result = await sut.GetEntries(EndpointKit.Request(EndpointKit.AdminId));

            EndpointKit.Status(result).Should().Be(400, "only a signed-in Admin may list without a season and pool");
        }

        private static UserFunctions BuildUserFunctions(Mock<IUserRepository>? users = null)
        {
            var config = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            return new UserFunctions(new NullLoggerFactory(), (users ?? EndpointKit.Users()).Object, config.Object);
        }

        [Fact]
        public async Task GetMe_ShouldReturn403WithNoProfile_WhenCallerIsDisabled()
        {
            var result = await BuildUserFunctions().GetMe(EndpointKit.Request(EndpointKit.DisabledSuperAdminId));

            EndpointKit.Status(result).Should().Be(403);
            EndpointKit.BodyText(result).Should().NotContain("SuperAdmin").And.NotContain("offsuper@test.com");
        }

        [Fact]
        public async Task GetMe_ShouldReturnProfile_WhenCallerIsEnabled()
        {
            var result = await BuildUserFunctions().GetMe(EndpointKit.Request(EndpointKit.SuperAdminId));

            EndpointKit.Status(result).Should().Be(200);
            EndpointKit.BodyText(result).Should().Contain("super@test.com");
        }

        [Fact]
        public async Task GetUsers_ShouldReturn401_WhenAdminIsDisabled()
        {
            var users = EndpointKit.Users();
            users.Object.GetUserAsync(EndpointKit.AdminId).Result!.IsDisabled = true;

            var result = await BuildUserFunctions(users).GetUsers(EndpointKit.Request(EndpointKit.AdminId));

            EndpointKit.Status(result).Should().Be(401);
        }

        [Fact]
        public async Task ToggleUserStatus_ShouldReturn401AndChangeNothing_WhenSuperAdminIsDisabled()
        {
            var users = EndpointKit.Users();

            var result = await BuildUserFunctions(users).ToggleUserStatus(EndpointKit.Request(EndpointKit.DisabledSuperAdminId, "?userId=" + EndpointKit.OtherPlayerId));

            EndpointKit.Status(result).Should().Be(401);
            users.Verify(u => u.UpsertUserAsync(It.IsAny<UserProfile>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUserRole_ShouldReturn401AndChangeNothing_WhenSuperAdminIsDisabled()
        {
            var users = EndpointKit.Users();

            var result = await BuildUserFunctions(users).UpdateUserRole(EndpointKit.Request(EndpointKit.DisabledSuperAdminId, "?userId=" + EndpointKit.PlayerId + "&role=Admin"));

            EndpointKit.Status(result).Should().Be(401);
            users.Verify(u => u.UpsertUserAsync(It.IsAny<UserProfile>()), Times.Never);
        }

        [Fact]
        public async Task AnalyzeLegacyData_ShouldRefuseAndReadNothing_WhenSuperAdminIsDisabled()
        {
            var migration = new Mock<IMigrationRepository>();
            var sut = new MigrationFunctions(NullLogger<MigrationFunctions>.Instance, migration.Object,
                new Mock<IEntryRepository>().Object, EndpointKit.Users().Object);

            var result = await sut.AnalyzeLegacyData(EndpointKit.AspRequest(EndpointKit.DisabledSuperAdminId));

            EndpointKit.StatusOf(result).Should().Be(401);
            migration.Verify(m => m.AnalyzeLegacyDataAsync(), Times.Never);
        }

        // The Score Feed admin endpoints (group A) rely on the shared SuperAdmin check.
        [Fact]
        public async Task MockFeedEndpoints_ShouldReturn401AndTouchNothing_WhenSuperAdminIsDisabled()
        {
            var store = NewMockFeedStore();
            var settings = new Mock<IScoreFeedSettingsStore>();
            var sut = new MockFeedFunctions(new NullLoggerFactory(), EndpointKit.Users().Object, store.Object,
                new Mock<IMockFeedStartingSet>().Object, settings.Object, new Mock<IMockFeedResetService>().Object,
                new Mock<IMockFeedImpactService>().Object);

            var mode = await sut.GetMockFeedMode(EndpointKit.Request(EndpointKit.DisabledSuperAdminId));
            var games = await sut.GetMockFeedGames(EndpointKit.Request(EndpointKit.DisabledSuperAdminId));
            var set = await sut.SetMockFeedMode(EndpointKit.Request(EndpointKit.DisabledSuperAdminId, body: new { mode = "Mock", confirm = "MOCK" }));
            var load = await sut.LoadMockFeedStartingSet(EndpointKit.Request(EndpointKit.DisabledSuperAdminId));

            new[] { mode, games, set, load }.Select(EndpointKit.Status).Should().OnlyContain(s => s == 401);
            store.Invocations.Should().BeEmpty();
            settings.Invocations.Should().BeEmpty();
        }
    }
}
