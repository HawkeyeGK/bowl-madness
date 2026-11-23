using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using BowlPoolManager.Api.Functions;
using BowlPoolManager.Api.Helpers;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Api.Services.ScoreFeed;
using BowlPoolManager.Core.ScoreFeed;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// ResetTestData deletes everything in Seasons, Picks and PoolArchives. These tests pin the order
    /// of the checks (caller, then guard, then scenario name, then delete) and that nothing is deleted
    /// unless every check passes. Endpoints are built from the account-name constants.
    /// </summary>
    public class TestDataFunctionsTests
    {
        private class Fixture
        {
            public Mock<IUserRepository> Users = new();
            public Mock<ITestEnvironment> Env = new();
            public Mock<ITestDataRepository> Data = new();
            public Mock<ITestScenarioProvider> Scenarios = new();
            public Mock<IScoreFeedSettingsStore> FeedSettings = new();

            public Fixture(bool testEnvironment)
            {
                Env.Setup(e => e.IsEnabled).Returns(testEnvironment);
                Data.Setup(d => d.DeleteAllAsync(It.IsAny<string>())).ReturnsAsync(3);
                Data.Setup(d => d.UpsertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JObject>())).Returns(Task.CompletedTask);
                Scenarios.Setup(s => s.ScenarioNames).Returns(new[] { "entry-window" });
                Scenarios.Setup(s => s.Get(It.IsAny<string>())).Returns(new TestScenario
                {
                    Name = "entry-window",
                    Documents = new List<TestDocument>
                    {
                        new() { Container = Constants.Database.SeasonsContainer, PartitionKey = "2025", Body = new JObject { ["id"] = "s1" } },
                        new() { Container = Constants.Database.SeasonsContainer, PartitionKey = "2025", Body = new JObject { ["id"] = "p1" } },
                        new() { Container = Constants.Database.ConfigurationContainer, PartitionKey = "Config_Teams_FBS", Body = new JObject { ["id"] = "Config_Teams_FBS" } }
                    }
                });
            }

            public TestDataFunctions Build() => new(
                new NullLoggerFactory(), Users.Object, Env.Object, Data.Object, Scenarios.Object, FeedSettings.Object);

            public void SuperAdmin(string id) => Users
                .Setup(u => u.GetUserAsync(It.IsAny<string>()))
                .ReturnsAsync(new UserProfile { AppRole = Constants.Roles.SuperAdmin });

            public void NoDataTouched()
            {
                Data.Verify(d => d.DeleteAllAsync(It.IsAny<string>()), Times.Never);
                Data.Verify(d => d.UpsertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JObject>()), Times.Never);
                FeedSettings.Verify(s => s.SetModeAsync(It.IsAny<ScoreFeedMode>()), Times.Never);
            }
        }

        private static (HttpRequestData req, HttpResponseData resp) BuildRequest(string? swaHeaderValue)
        {
            var ctx = new Mock<FunctionContext>().Object;
            var headers = new HttpHeadersCollection();
            if (swaHeaderValue != null)
                headers.Add("x-ms-client-principal", swaHeaderValue);

            var requestMock = new Mock<HttpRequestData>(ctx);
            requestMock.Setup(r => r.Headers).Returns(headers);

            var body = new MemoryStream();
            var responseMock = new Mock<HttpResponseData>(ctx);
            responseMock.SetupProperty(r => r.StatusCode);
            responseMock.SetupProperty(r => r.Headers, new HttpHeadersCollection());
            responseMock.Setup(r => r.Body).Returns(body);
            requestMock.Setup(r => r.CreateResponse()).Returns(responseMock.Object);

            return (requestMock.Object, responseMock.Object);
        }

        private static string Principal(string userId) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                userId,
                userDetails = "someone@test.com",
                identityProvider = "aad",
                userRoles = new[] { "authenticated" }
            })));

        // ── Caller check comes first ──────────────────────────────────────────────

        [Fact]
        public async Task ResetTestData_ShouldReturn401AndNotReadGuard_WhenAuthHeaderMissing()
        {
            var f = new Fixture(testEnvironment: false);
            var (req, _) = BuildRequest(null);

            var result = await f.Build().ResetTestData(req, "entry-window");

            result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            f.Env.Verify(e => e.IsEnabled, Times.Never);
            f.NoDataTouched();
        }

        [Fact]
        public async Task ResetTestData_ShouldReturn401_WhenAuthHeaderMissingOnTestSite()
        {
            var f = new Fixture(testEnvironment: true);
            var (req, _) = BuildRequest(null);

            var result = await f.Build().ResetTestData(req, "entry-window");

            result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            f.NoDataTouched();
        }

        [Theory]
        [InlineData(Constants.Roles.Player)]
        [InlineData(Constants.Roles.Admin)]
        public async Task ResetTestData_ShouldReturn403AndNotReadGuard_WhenCallerIsNotSuperAdmin(string role)
        {
            var f = new Fixture(testEnvironment: false);
            f.Users.Setup(u => u.GetUserAsync(It.IsAny<string>())).ReturnsAsync(new UserProfile { AppRole = role });
            var (req, _) = BuildRequest(Principal("user-1"));

            var result = await f.Build().ResetTestData(req, "entry-window");

            result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            f.Env.Verify(e => e.IsEnabled, Times.Never);
            f.NoDataTouched();
        }

        [Theory]
        [InlineData(Constants.Roles.Player)]
        [InlineData(Constants.Roles.Admin)]
        public async Task ResetTestData_ShouldReturn403_WhenCallerIsNotSuperAdminOnTestSite(string role)
        {
            var f = new Fixture(testEnvironment: true);
            f.Users.Setup(u => u.GetUserAsync(It.IsAny<string>())).ReturnsAsync(new UserProfile { AppRole = role });
            var (req, _) = BuildRequest(Principal("user-1"));

            var result = await f.Build().ResetTestData(req, "entry-window");

            result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            f.NoDataTouched();
        }

        [Fact]
        public async Task ResetTestData_ShouldReturn403_WhenCallerHasNoProfile()
        {
            var f = new Fixture(testEnvironment: true);
            f.Users.Setup(u => u.GetUserAsync(It.IsAny<string>())).ReturnsAsync((UserProfile?)null);
            var (req, _) = BuildRequest(Principal("ghost"));

            var result = await f.Build().ResetTestData(req, "entry-window");

            result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            f.NoDataTouched();
        }

        // ── Guard ─────────────────────────────────────────────────────────────────

        [Fact]
        public async Task ResetTestData_ShouldReturn403AndDeleteNothing_WhenSuperAdminButNotTestEnvironment()
        {
            var f = new Fixture(testEnvironment: false);
            f.SuperAdmin("owner");
            var (req, _) = BuildRequest(Principal("owner"));

            var result = await f.Build().ResetTestData(req, "entry-window");

            result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            f.NoDataTouched();
            f.Scenarios.Verify(s => s.Get(It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData("Test", true)]   // setting right, production account
        [InlineData(null, false)]    // setting missing, test account
        [InlineData("Production", false)]
        [InlineData("test", false)]
        public async Task ResetTestData_ShouldReturn403AndDeleteNothing_WhenRealGuardRefuses(string? siteEnvironment, bool useProductionAccount)
        {
            var account = useProductionAccount ? TestEnvironment.ProductionAccountName : TestEnvironment.TestAccountName;
            var endpoint = new Uri($"https://{account}.{TestEnvironment.CosmosHostSuffix}:443/");

            var f = new Fixture(testEnvironment: true);
            f.SuperAdmin("owner");
            var sut = new TestDataFunctions(
                new NullLoggerFactory(), f.Users.Object, new TestEnvironment(siteEnvironment, endpoint), f.Data.Object, f.Scenarios.Object, f.FeedSettings.Object);
            var (req, _) = BuildRequest(Principal("owner"));

            var result = await sut.ResetTestData(req, "entry-window");

            result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            f.NoDataTouched();
        }

        // ── Scenario name ─────────────────────────────────────────────────────────

        [Theory]
        [InlineData("no-such-scenario")]
        [InlineData("")]
        [InlineData("../entry-window")]
        public async Task ResetTestData_ShouldReturn400AndDeleteNothing_WhenScenarioIsUnknown(string scenario)
        {
            var f = new Fixture(testEnvironment: true);
            f.SuperAdmin("owner");
            f.Scenarios.Setup(s => s.Get(It.IsAny<string>())).Returns((TestScenario?)null);
            var (req, _) = BuildRequest(Principal("owner"));

            var result = await f.Build().ResetTestData(req, scenario);

            result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            f.NoDataTouched();
        }

        // ── Success ───────────────────────────────────────────────────────────────

        [Fact]
        public async Task ResetTestData_ShouldDeleteOnlySeasonsPicksAndArchives_WhenSuperAdminOnTestSite()
        {
            var f = new Fixture(testEnvironment: true);
            f.SuperAdmin("owner");
            var (req, _) = BuildRequest(Principal("owner"));

            var result = await f.Build().ResetTestData(req, "entry-window");

            result.StatusCode.Should().Be(HttpStatusCode.OK);
            f.Data.Verify(d => d.DeleteAllAsync(Constants.Database.SeasonsContainer), Times.Once);
            f.Data.Verify(d => d.DeleteAllAsync(Constants.Database.PicksContainer), Times.Once);
            f.Data.Verify(d => d.DeleteAllAsync(Constants.Database.ArchivesContainer), Times.Once);
            f.Data.Verify(d => d.DeleteAllAsync(Constants.Database.PlayersContainer), Times.Never);
            f.Data.Verify(d => d.DeleteAllAsync(Constants.Database.ConfigurationContainer), Times.Never);
        }

        [Fact]
        public async Task ResetTestData_ShouldDeleteBeforeLoadingScenario_WhenSuperAdminOnTestSite()
        {
            var f = new Fixture(testEnvironment: true);
            f.SuperAdmin("owner");
            var calls = new List<string>();
            f.Data.Setup(d => d.DeleteAllAsync(It.IsAny<string>())).Callback<string>(c => calls.Add("delete:" + c)).ReturnsAsync(0);
            f.Data.Setup(d => d.UpsertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JObject>()))
                .Callback<string, string, JObject>((c, _, _) => calls.Add("upsert:" + c)).Returns(Task.CompletedTask);
            var (req, _) = BuildRequest(Principal("owner"));

            await f.Build().ResetTestData(req, "entry-window");

            calls.Should().HaveCount(6);
            calls.Take(3).Should().OnlyContain(c => c.StartsWith("delete:"));
            calls.Skip(3).Should().OnlyContain(c => c.StartsWith("upsert:"));
        }

        [Fact]
        public async Task ResetTestData_ShouldSetTheScoreFeedBackToLiveAfterLoading_WhenSuperAdminOnTestSite()
        {
            var f = new Fixture(testEnvironment: true);
            f.SuperAdmin("owner");
            var calls = new List<string>();
            f.Data.Setup(d => d.UpsertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JObject>()))
                .Callback<string, string, JObject>((_, _, _) => calls.Add("upsert")).Returns(Task.CompletedTask);
            f.FeedSettings.Setup(s => s.SetModeAsync(ScoreFeedMode.Live)).Callback(() => calls.Add("live")).Returns(Task.CompletedTask);
            var (req, _) = BuildRequest(Principal("owner"));

            await f.Build().ResetTestData(req, "entry-window");

            f.FeedSettings.Verify(s => s.SetModeAsync(ScoreFeedMode.Live), Times.Once);
            calls.Last().Should().Be("live");
        }

        [Fact]
        public async Task ResetTestData_ShouldNotChangeTheScoreFeed_WhenScenarioIsUnknown()
        {
            var f = new Fixture(testEnvironment: true);
            f.SuperAdmin("owner");
            f.Scenarios.Setup(s => s.Get(It.IsAny<string>())).Returns((TestScenario?)null);
            var (req, _) = BuildRequest(Principal("owner"));

            await f.Build().ResetTestData(req, "nope");

            f.FeedSettings.Verify(s => s.SetModeAsync(It.IsAny<ScoreFeedMode>()), Times.Never);
        }

        [Fact]
        public async Task ResetTestData_ShouldReturnCountsInBody_WhenSuperAdminOnTestSite()
        {
            var f = new Fixture(testEnvironment: true);
            f.SuperAdmin("owner");
            var (req, resp) = BuildRequest(Principal("owner"));

            var result = await f.Build().ResetTestData(req, "entry-window");

            result.Body.Position = 0;
            using var doc = JsonDocument.Parse(new StreamReader(result.Body).ReadToEnd());
            var root = doc.RootElement;
            root.GetProperty("scenario").GetString().Should().Be("entry-window");
            root.GetProperty("deleted").GetProperty("Seasons").GetInt32().Should().Be(3);
            root.GetProperty("loaded").GetProperty("Seasons").GetInt32().Should().Be(2);
            root.GetProperty("loaded").GetProperty("Configuration").GetInt32().Should().Be(1);
        }

        // ── Status ────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetTestEnvironmentStatus_ShouldReturn401_WhenAuthHeaderMissing()
        {
            var f = new Fixture(testEnvironment: true);
            var (req, _) = BuildRequest(null);

            var result = await f.Build().GetTestEnvironmentStatus(req);

            result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetTestEnvironmentStatus_ShouldReportDisabledAndNoScenarios_WhenNotTestEnvironment()
        {
            var f = new Fixture(testEnvironment: false);
            f.SuperAdmin("owner");
            var (req, _) = BuildRequest(Principal("owner"));

            var result = await f.Build().GetTestEnvironmentStatus(req);

            result.StatusCode.Should().Be(HttpStatusCode.OK);
            result.Body.Position = 0;
            using var doc = JsonDocument.Parse(new StreamReader(result.Body).ReadToEnd());
            doc.RootElement.GetProperty("isTestEnvironment").GetBoolean().Should().BeFalse();
            doc.RootElement.GetProperty("scenarios").GetArrayLength().Should().Be(0);
        }
    }
}
