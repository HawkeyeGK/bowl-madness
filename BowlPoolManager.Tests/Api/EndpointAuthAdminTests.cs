using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BowlPoolManager.Api.Functions;
using BowlPoolManager.Api.Functions.Admin;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// Sign-in and role tests for the SuperAdmin integration, sync and logo endpoints
    /// (BUSINESS-RULES section 1: integrations and team syncs are SuperAdmin).
    /// </summary>
    public class EndpointAuthIntegrationTests
    {
        // ── ApplyLogoMappings (POST) ──────────────────────────────────────────────

        private static (ApplyLogoMappings sut, Mock<IConfigurationRepository> config) BuildLogoMappings()
        {
            var config = new Mock<IConfigurationRepository>();
            config.Setup(c => c.GetBasketballTeamConfigAsync()).ReturnsAsync(new TeamConfig
            {
                Teams = new List<TeamInfo> { new() { SchoolId = 1, School = "Test U" } }
            });
            var sut = new ApplyLogoMappings(config.Object, NullLogger<ApplyLogoMappings>.Instance, EndpointKit.Users().Object);
            return (sut, config);
        }

        private static List<LogoMappingDto> OneMapping() => new() { new LogoMappingDto { SchoolId = 1, LogoUrl = "https://example.test/logo.png" } };

        [Theory]
        [InlineData(Tier.Anonymous, 401)]
        [InlineData(Tier.Player, 403)]
        [InlineData(Tier.Admin, 403)]
        public async Task ApplyLogoMappings_ShouldRefuseAndSaveNothing_WhenCallerIsNotSuperAdmin(Tier tier, int expected)
        {
            var (sut, config) = BuildLogoMappings();

            var result = await sut.Run(EndpointKit.Request(tier, body: OneMapping()));

            EndpointKit.Status(result).Should().Be(expected);
            config.Verify(c => c.SaveBasketballTeamConfigAsync(It.IsAny<TeamConfig>()), Times.Never);
        }

        [Fact]
        public async Task ApplyLogoMappings_ShouldSaveConfig_WhenSuperAdmin()
        {
            var (sut, config) = BuildLogoMappings();

            var result = await sut.Run(EndpointKit.Request(Tier.SuperAdmin, body: OneMapping()));

            EndpointKit.Status(result).Should().Be(200);
            config.Verify(c => c.SaveBasketballTeamConfigAsync(It.IsAny<TeamConfig>()), Times.Once);
        }

        // ── SyncBasketballTeams (POST) ────────────────────────────────────────────

        private static (SyncBasketballTeams sut, Mock<IBasketballDataService> feed, Mock<IConfigurationRepository> config) BuildBasketballSync()
        {
            var feed = new Mock<IBasketballDataService>();
            feed.Setup(b => b.GetTeamsAsync()).ReturnsAsync(new List<TeamInfo> { new() { SchoolId = 1, School = "Test U", Conference = "Test" } });
            var config = new Mock<IConfigurationRepository>();
            config.Setup(c => c.GetTeamConfigAsync()).ReturnsAsync((TeamConfig?)null);
            var sut = new SyncBasketballTeams(feed.Object, config.Object, NullLogger<SyncBasketballTeams>.Instance, EndpointKit.Users().Object);
            return (sut, feed, config);
        }

        [Theory]
        [InlineData(Tier.Anonymous, 401)]
        [InlineData(Tier.Player, 403)]
        [InlineData(Tier.Admin, 403)]
        public async Task SyncBasketballTeams_ShouldRefuseAndSyncNothing_WhenCallerIsNotSuperAdmin(Tier tier, int expected)
        {
            var (sut, feed, config) = BuildBasketballSync();

            var result = await sut.Run(EndpointKit.Request(tier));

            EndpointKit.Status(result).Should().Be(expected);
            feed.Verify(b => b.GetTeamsAsync(), Times.Never);
            config.Verify(c => c.SaveBasketballTeamConfigAsync(It.IsAny<TeamConfig>()), Times.Never);
        }

        [Fact]
        public async Task SyncBasketballTeams_ShouldSaveTeamConfig_WhenSuperAdmin()
        {
            var (sut, _, config) = BuildBasketballSync();

            var result = await sut.Run(EndpointKit.Request(Tier.SuperAdmin));

            EndpointKit.Status(result).Should().Be(200);
            config.Verify(c => c.SaveBasketballTeamConfigAsync(It.IsAny<TeamConfig>()), Times.Once);
        }

        // ── SyncEspnLogos (POST) ──────────────────────────────────────────────────

        private static (SyncEspnLogos sut, Mock<IEspnDataService> feed, Mock<IConfigurationRepository> config) BuildEspnSync()
        {
            var feed = new Mock<IEspnDataService>();
            feed.Setup(e => e.GetTeamsAsync()).ReturnsAsync(new List<TeamInfo>
            {
                new() { School = "Test U", Logos = new List<string> { "https://example.test/logo.png" } }
            });
            var config = new Mock<IConfigurationRepository>();
            config.Setup(c => c.GetBasketballTeamConfigAsync()).ReturnsAsync(new TeamConfig
            {
                Teams = new List<TeamInfo> { new() { SchoolId = 1, School = "Test U" } }
            });
            var sut = new SyncEspnLogos(feed.Object, config.Object, NullLogger<SyncEspnLogos>.Instance, EndpointKit.Users().Object);
            return (sut, feed, config);
        }

        [Theory]
        [InlineData(Tier.Anonymous, 401)]
        [InlineData(Tier.Player, 403)]
        [InlineData(Tier.Admin, 403)]
        public async Task SyncEspnLogos_ShouldRefuseAndSyncNothing_WhenCallerIsNotSuperAdmin(Tier tier, int expected)
        {
            var (sut, feed, config) = BuildEspnSync();

            var result = await sut.Run(EndpointKit.Request(tier));

            EndpointKit.Status(result).Should().Be(expected);
            feed.Verify(e => e.GetTeamsAsync(), Times.Never);
            config.Verify(c => c.SaveBasketballTeamConfigAsync(It.IsAny<TeamConfig>()), Times.Never);
        }

        [Fact]
        public async Task SyncEspnLogos_ShouldSaveTeamConfig_WhenSuperAdmin()
        {
            var (sut, _, config) = BuildEspnSync();

            var result = await sut.Run(EndpointKit.Request(Tier.SuperAdmin));

            EndpointKit.Status(result).Should().Be(200);
            config.Verify(c => c.SaveBasketballTeamConfigAsync(It.IsAny<TeamConfig>()), Times.Once);
        }

        // ── GetIntegrationStatus (GET, SuperAdmin only; a read, covered for the tier check) ──

        private static AdminDashboardFunctions BuildDashboard()
        {
            var cosmos = new Mock<CosmosClient>();
            cosmos.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(new Mock<Container>().Object);
            return new AdminDashboardFunctions(cosmos.Object, NullLogger<AdminDashboardFunctions>.Instance, EndpointKit.Users().Object);
        }

        [Theory]
        [InlineData(Tier.Anonymous, 401)]
        [InlineData(Tier.Player, 403)]
        [InlineData(Tier.Admin, 403)]
        public async Task GetIntegrationStatus_ShouldRefuse_WhenCallerIsNotSuperAdmin(Tier tier, int expected)
        {
            var result = await BuildDashboard().GetIntegrationStatus(EndpointKit.Request(tier));

            EndpointKit.Status(result).Should().Be(expected);
        }

        [Fact]
        public async Task GetIntegrationStatus_ShouldReturn200_WhenSuperAdmin()
        {
            var result = await BuildDashboard().GetIntegrationStatus(EndpointKit.Request(Tier.SuperAdmin));

            EndpointKit.Status(result).Should().Be(200);
        }

        // ── #93: SyncTeams, InitializeDatabase, GetBackupData and SaveSeason are SuperAdmin only ─────

        [Theory]
        [InlineData(Tier.Anonymous, 401)]
        [InlineData(Tier.Player, 403)]
        [InlineData(Tier.Admin, 403)]
        public async Task SyncTeams_ShouldRefuseAndCallNothing_WhenCallerIsNotSuperAdmin(Tier tier, int expected)
        {
            var cfbd = new Mock<ICfbdService>();
            cfbd.Setup(c => c.GetFbsTeamsAsync()).ReturnsAsync(new List<TeamInfo> { new() { School = "Test U", Conference = "Test" } });
            var cosmos = new Mock<CosmosClient> { DefaultValue = DefaultValue.Mock };
            var sut = new SyncTeams(cfbd.Object, cosmos.Object, NullLogger<SyncTeams>.Instance, EndpointKit.Users().Object);

            var result = await sut.Run(EndpointKit.AspRequest(tier));

            EndpointKit.StatusOf(result).Should().Be(expected);
            cfbd.Verify(c => c.GetFbsTeamsAsync(), Times.Never);
        }

        [Fact]
        public async Task SyncTeams_ShouldCallTheTeamService_WhenSuperAdmin()
        {
            var cfbd = new Mock<ICfbdService>();
            cfbd.Setup(c => c.GetFbsTeamsAsync()).ReturnsAsync(new List<TeamInfo> { new() { School = "Test U", Conference = "Test" } });
            var cosmos = new Mock<CosmosClient> { DefaultValue = DefaultValue.Mock };
            var sut = new SyncTeams(cfbd.Object, cosmos.Object, NullLogger<SyncTeams>.Instance, EndpointKit.Users().Object);

            await sut.Run(EndpointKit.AspRequest(Tier.SuperAdmin));

            cfbd.Verify(c => c.GetFbsTeamsAsync(), Times.Once);
        }

        // The route also never registers today (#120), so this guards the day it is repaired.
        [Theory]
        [InlineData(Tier.Anonymous, 401)]
        [InlineData(Tier.Player, 403)]
        [InlineData(Tier.Admin, 403)]
        public async Task InitializeDatabase_ShouldRefuseAndTouchNothing_WhenCallerIsNotSuperAdmin(Tier tier, int expected)
        {
            var cosmos = new Mock<CosmosClient> { DefaultValue = DefaultValue.Mock };
            var sut = new InfrastructureFunctions(cosmos.Object, NullLogger<InfrastructureFunctions>.Instance, EndpointKit.Users().Object);

            var result = await sut.InitializeDatabase(EndpointKit.Request(tier));

            EndpointKit.Status(result).Should().Be(expected);
            cosmos.Invocations.Should().BeEmpty("a refused caller must not reach the database");
        }

        [Fact]
        public async Task InitializeDatabase_ShouldReachTheDatabase_WhenSuperAdmin()
        {
            var cosmos = new Mock<CosmosClient> { DefaultValue = DefaultValue.Mock };
            var sut = new InfrastructureFunctions(cosmos.Object, NullLogger<InfrastructureFunctions>.Instance, EndpointKit.Users().Object);

            await sut.InitializeDatabase(EndpointKit.Request(Tier.SuperAdmin));

            cosmos.Invocations.Should().NotBeEmpty();
        }

        // The backup holds every football entry's picks and user ids.
        [Theory]
        [InlineData(Tier.Anonymous, 401)]
        [InlineData(Tier.Player, 403)]
        [InlineData(Tier.Admin, 403)]
        public async Task GetBackupData_ShouldRefuseAndReadNothing_WhenCallerIsNotSuperAdmin(Tier tier, int expected)
        {
            // A plain mock: the private DTO types of the function cannot be proxied, so a call that got past the check
            // would throw once it reached the database. That is captured so the assertions below decide.
            var cosmos = new Mock<CosmosClient>();
            var sut = new BackupFunctions(cosmos.Object, NullLogger<BackupFunctions>.Instance, EndpointKit.Users().Object);
            Microsoft.AspNetCore.Mvc.IActionResult? result = null;

            await Record.ExceptionAsync(async () => result = await sut.Run(EndpointKit.AspRequest(tier)));

            cosmos.Invocations.Should().BeEmpty("a refused caller must not reach the database");
            result.Should().NotBeNull();
            EndpointKit.StatusOf(result!).Should().Be(expected);
        }

        [Fact]
        public async Task GetBackupData_ShouldReachTheDatabase_WhenSuperAdmin()
        {
            var cosmos = new Mock<CosmosClient>();
            var sut = new BackupFunctions(cosmos.Object, NullLogger<BackupFunctions>.Instance, EndpointKit.Users().Object);

            await Record.ExceptionAsync(async () => await sut.Run(EndpointKit.AspRequest(Tier.SuperAdmin)));

            cosmos.Invocations.Should().NotBeEmpty();
        }

        // Anyone could switch the current season for everyone.
        [Theory]
        [InlineData(Tier.Anonymous, 401)]
        [InlineData(Tier.Player, 403)]
        [InlineData(Tier.Admin, 403)]
        public async Task SaveSeason_ShouldRefuseAndSaveNothing_WhenCallerIsNotSuperAdmin(Tier tier, int expected)
        {
            var seasons = new Mock<ISeasonRepository>();
            var sut = new SeasonFunctions(seasons.Object, NullLogger<SeasonFunctions>.Instance, EndpointKit.Users().Object);
            var body = new Season { SeasonId = "2026", Name = "2026 Season", IsCurrent = true };

            var result = await sut.SaveSeason(EndpointKit.AspRequest(tier, body));

            EndpointKit.StatusOf(result).Should().Be(expected);
            seasons.Verify(s => s.UpsertSeasonAsync(It.IsAny<Season>()), Times.Never);
        }

        [Fact]
        public async Task SaveSeason_ShouldSaveSeason_WhenSuperAdmin()
        {
            var seasons = new Mock<ISeasonRepository>();
            var sut = new SeasonFunctions(seasons.Object, NullLogger<SeasonFunctions>.Instance, EndpointKit.Users().Object);
            var body = new Season { SeasonId = "2026", Name = "2026 Season", IsCurrent = true };

            var result = await sut.SaveSeason(EndpointKit.AspRequest(Tier.SuperAdmin, body));

            EndpointKit.StatusOf(result).Should().Be(200);
            seasons.Verify(s => s.UpsertSeasonAsync(It.IsAny<Season>()), Times.Once);
        }

        // A disabled SuperAdmin is refused like an anonymous caller, on the ASP.NET-style functions too (#98).
        [Fact]
        public async Task SaveSeason_ShouldReturn401_WhenSuperAdminIsDisabled()
        {
            var seasons = new Mock<ISeasonRepository>();
            var sut = new SeasonFunctions(seasons.Object, NullLogger<SeasonFunctions>.Instance, EndpointKit.Users().Object);
            var body = new Season { SeasonId = "2026", Name = "2026 Season", IsCurrent = true };

            var result = await sut.SaveSeason(EndpointKit.AspRequest(EndpointKit.DisabledSuperAdminId, body));

            EndpointKit.StatusOf(result).Should().Be(401);
            seasons.Verify(s => s.UpsertSeasonAsync(It.IsAny<Season>()), Times.Never);
        }
    }

    /// <summary>
    /// Sign-in and role tests for the legacy migration endpoints (SuperAdmin only).
    /// MigrationFunctions answers 403 for a caller with no sign-in instead of 401, so the anonymous
    /// tier is asserted as "refused and nothing touched" rather than a specific status.
    /// </summary>
    public class EndpointAuthMigrationTests
    {
        private static (MigrationFunctions sut, Mock<IMigrationRepository> migration) Build()
        {
            var migration = new Mock<IMigrationRepository>();
            migration.Setup(m => m.AnalyzeLegacyDataAsync()).ReturnsAsync((
                new List<LegacyGameDto>(), new List<string>(), new List<string>(), new List<LegacyPoolDto>(), 0, ""));
            migration.Setup(m => m.GetLegacyEntriesAsync()).ReturnsAsync(new List<dynamic>());
            var sut = new MigrationFunctions(NullLogger<MigrationFunctions>.Instance, migration.Object,
                new Mock<IEntryRepository>().Object, EndpointKit.Users().Object);
            return (sut, migration);
        }

        // ── AnalyzeLegacyData (POST) ──────────────────────────────────────────────

        [Fact]
        public async Task AnalyzeLegacyData_ShouldRefuseAndReadNothing_WhenAnonymous()
        {
            var (sut, migration) = Build();

            var result = await sut.AnalyzeLegacyData(EndpointKit.AspRequest(Tier.Anonymous));

            EndpointKit.StatusOf(result).Should().BeOneOf(401, 403);
            migration.Verify(m => m.AnalyzeLegacyDataAsync(), Times.Never);
        }

        [Theory]
        [InlineData(Tier.Player)]
        [InlineData(Tier.Admin)]
        public async Task AnalyzeLegacyData_ShouldReturn403_WhenCallerIsNotSuperAdmin(Tier tier)
        {
            var (sut, migration) = Build();

            var result = await sut.AnalyzeLegacyData(EndpointKit.AspRequest(tier));

            EndpointKit.StatusOf(result).Should().Be(403);
            migration.Verify(m => m.AnalyzeLegacyDataAsync(), Times.Never);
        }

        [Fact]
        public async Task AnalyzeLegacyData_ShouldAnalyze_WhenSuperAdmin()
        {
            var (sut, migration) = Build();

            var result = await sut.AnalyzeLegacyData(EndpointKit.AspRequest(Tier.SuperAdmin));

            EndpointKit.StatusOf(result).Should().Be(200);
            migration.Verify(m => m.AnalyzeLegacyDataAsync(), Times.Once);
        }

        // ── ExecuteMigration (POST) ───────────────────────────────────────────────

        [Fact]
        public async Task ExecuteMigration_ShouldRefuseAndMigrateNothing_WhenAnonymous()
        {
            var (sut, migration) = Build();

            var result = await sut.ExecuteMigration(EndpointKit.AspRequest(Tier.Anonymous, new MigrationExecutionRequest()));

            EndpointKit.StatusOf(result).Should().BeOneOf(401, 403);
            migration.Verify(m => m.GetLegacyEntriesAsync(), Times.Never);
        }

        [Theory]
        [InlineData(Tier.Player)]
        [InlineData(Tier.Admin)]
        public async Task ExecuteMigration_ShouldReturn403_WhenCallerIsNotSuperAdmin(Tier tier)
        {
            var (sut, migration) = Build();

            var result = await sut.ExecuteMigration(EndpointKit.AspRequest(tier, new MigrationExecutionRequest()));

            EndpointKit.StatusOf(result).Should().Be(403);
            migration.Verify(m => m.GetLegacyEntriesAsync(), Times.Never);
        }

        [Fact]
        public async Task ExecuteMigration_ShouldReadLegacyEntries_WhenSuperAdmin()
        {
            var (sut, migration) = Build();

            var result = await sut.ExecuteMigration(EndpointKit.AspRequest(Tier.SuperAdmin, new MigrationExecutionRequest()));

            EndpointKit.StatusOf(result).Should().Be(200);
            migration.Verify(m => m.GetLegacyEntriesAsync(), Times.Once);
        }
    }

    /// <summary>
    /// Sign-in and role tests for the user endpoints. BUSINESS-RULES section 1: only a SuperAdmin
    /// changes roles or disables accounts; an Admin may view the user list.
    /// </summary>
    public class EndpointAuthUserTests
    {
        private static (UserFunctions sut, Mock<IUserRepository> users) Build(string? bootstrapEmail = null)
        {
            var users = EndpointKit.Users();
            var config = new Mock<IConfiguration>();
            config.Setup(c => c["BootstrapAdminEmail"]).Returns(bootstrapEmail);
            return (new UserFunctions(new NullLoggerFactory(), users.Object, config.Object), users);
        }

        // ── ToggleUserStatus (POST) ───────────────────────────────────────────────

        private const string ToggleQuery = "?userId=" + EndpointKit.OtherPlayerId;

        [Fact]
        public async Task ToggleUserStatus_ShouldReturn401_WhenAnonymous()
        {
            var (sut, users) = Build();

            var result = await sut.ToggleUserStatus(EndpointKit.Request(Tier.Anonymous, ToggleQuery));

            EndpointKit.Status(result).Should().Be(401);
            users.Verify(u => u.UpsertUserAsync(It.IsAny<UserProfile>()), Times.Never);
        }

        [Theory]
        [InlineData(Tier.Player)]
        [InlineData(Tier.Admin)]
        public async Task ToggleUserStatus_ShouldReturn403_WhenCallerIsNotSuperAdmin(Tier tier)
        {
            var (sut, users) = Build();

            var result = await sut.ToggleUserStatus(EndpointKit.Request(tier, ToggleQuery));

            EndpointKit.Status(result).Should().Be(403);
            users.Verify(u => u.UpsertUserAsync(It.IsAny<UserProfile>()), Times.Never);
        }

        [Fact]
        public async Task ToggleUserStatus_ShouldDisableUser_WhenSuperAdmin()
        {
            var (sut, users) = Build();

            var result = await sut.ToggleUserStatus(EndpointKit.Request(Tier.SuperAdmin, ToggleQuery));

            EndpointKit.Status(result).Should().Be(200);
            users.Verify(u => u.UpsertUserAsync(It.Is<UserProfile>(p => p.Id == EndpointKit.OtherPlayerId && p.IsDisabled)), Times.Once);
        }

        // ── UpdateUserRole (POST) ─────────────────────────────────────────────────

        private const string RoleQuery = "?userId=" + EndpointKit.PlayerId + "&role=" + Constants.Roles.Admin;

        [Fact]
        public async Task UpdateUserRole_ShouldReturn401_WhenAnonymous()
        {
            var (sut, users) = Build();

            var result = await sut.UpdateUserRole(EndpointKit.Request(Tier.Anonymous, RoleQuery));

            EndpointKit.Status(result).Should().Be(401);
            users.Verify(u => u.UpsertUserAsync(It.IsAny<UserProfile>()), Times.Never);
        }

        [Theory]
        [InlineData(Tier.Player)]
        [InlineData(Tier.Admin)]
        public async Task UpdateUserRole_ShouldReturn403_WhenCallerIsNotSuperAdmin(Tier tier)
        {
            var (sut, users) = Build();

            var result = await sut.UpdateUserRole(EndpointKit.Request(tier, RoleQuery));

            EndpointKit.Status(result).Should().Be(403);
            users.Verify(u => u.UpsertUserAsync(It.IsAny<UserProfile>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUserRole_ShouldChangeRole_WhenSuperAdmin()
        {
            var (sut, users) = Build();

            var result = await sut.UpdateUserRole(EndpointKit.Request(Tier.SuperAdmin, RoleQuery));

            EndpointKit.Status(result).Should().Be(200);
            users.Verify(u => u.UpsertUserAsync(It.Is<UserProfile>(p => p.Id == EndpointKit.PlayerId && p.AppRole == Constants.Roles.Admin)), Times.Once);
        }

        // ── GetUsers (GET; Admin may view the list) ───────────────────────────────

        [Theory]
        [InlineData(Tier.Anonymous, 401)]
        [InlineData(Tier.Player, 403)]
        public async Task GetUsers_ShouldRefuse_WhenCallerIsNotAdmin(Tier tier, int expected)
        {
            var (sut, users) = Build();

            var result = await sut.GetUsers(EndpointKit.Request(tier));

            EndpointKit.Status(result).Should().Be(expected);
            users.Verify(u => u.GetUsersAsync(), Times.Never);
        }

        [Theory]
        [InlineData(Tier.Admin)]
        [InlineData(Tier.SuperAdmin)]
        public async Task GetUsers_ShouldReturnList_WhenCallerIsAdminOrAbove(Tier tier)
        {
            var (sut, users) = Build();

            var result = await sut.GetUsers(EndpointKit.Request(tier));

            EndpointKit.Status(result).Should().Be(200);
            users.Verify(u => u.GetUsersAsync(), Times.Once);
        }

        // ── GetMe (GET; creates the profile on first sign-in) ─────────────────────

        [Fact]
        public async Task GetMe_ShouldReturn401AndCreateNothing_WhenAnonymous()
        {
            var (sut, users) = Build();

            var result = await sut.GetMe(EndpointKit.Request(Tier.Anonymous));

            EndpointKit.Status(result).Should().Be(401);
            users.Verify(u => u.UpsertUserAsync(It.IsAny<UserProfile>()), Times.Never);
        }

        [Fact]
        public async Task GetMe_ShouldCreatePlayerProfile_WhenNewUserSignsInWithoutBootstrapEmail()
        {
            var (sut, users) = Build(bootstrapEmail: "owner@test.com");

            var result = await sut.GetMe(EndpointKit.Request("brand-new-user", userDetails: "someone@test.com"));

            EndpointKit.Status(result).Should().Be(200);
            users.Verify(u => u.UpsertUserAsync(It.Is<UserProfile>(p => p.Id == "brand-new-user" && p.AppRole == Constants.Roles.Player)), Times.Once);
        }

        [Fact]
        public async Task GetMe_ShouldCreateSuperAdminProfile_WhenNewUserHasBootstrapEmail()
        {
            var (sut, users) = Build(bootstrapEmail: "owner@test.com");

            var result = await sut.GetMe(EndpointKit.Request("brand-new-owner", userDetails: "Owner@Test.com"));

            EndpointKit.Status(result).Should().Be(200);
            users.Verify(u => u.UpsertUserAsync(It.Is<UserProfile>(p => p.Id == "brand-new-owner" && p.AppRole == Constants.Roles.SuperAdmin)), Times.Once);
        }

        [Fact]
        public async Task GetMe_ShouldNotChangeRole_WhenExistingPlayerHasBootstrapEmail()
        {
            var (sut, users) = Build(bootstrapEmail: "player1@test.com"); // seeded Player profile uses this email

            var result = await sut.GetMe(EndpointKit.Request(EndpointKit.PlayerId, userDetails: "player1@test.com"));

            EndpointKit.Status(result).Should().Be(200);
            users.Verify(u => u.UpsertUserAsync(It.IsAny<UserProfile>()), Times.Never);
        }
    }
}
