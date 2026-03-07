using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using BowlPoolManager.Client.Services;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Client
{
    /// <summary>
    /// Stub HttpMessageHandler that tracks invocation count and returns a pre-set response.
    /// </summary>
    internal sealed class CountingStubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;
        public int CallCount { get; private set; }

        public CountingStubHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    public class ConfigurationServiceBasketballTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────────

        private static string SerializeConfig(List<TeamInfo> teams)
        {
            var config = new TeamConfig { Teams = teams };
            return JsonSerializer.Serialize(config);
        }

        private static (ConfigurationService sut, CountingStubHandler handler) Build(
            HttpStatusCode statusCode, string body)
        {
            var handler = new CountingStubHandler(statusCode, body);
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
            return (new ConfigurationService(httpClient), handler);
        }

        // ── GetBasketballTeamsAsync — happy path ──────────────────────────────

        [Fact]
        public async Task GetBasketballTeamsAsync_ShouldReturnTeams_WhenApiReturnsValidConfig()
        {
            var teams = new List<TeamInfo>
            {
                new() { SchoolId = 1, School = "Duke", Mascot = "Blue Devils", Conference = "ACC" },
                new() { SchoolId = 2, School = "Kansas", Mascot = "Jayhawks", Conference = "Big 12" }
            };
            var (sut, _) = Build(HttpStatusCode.OK, SerializeConfig(teams));

            var result = await sut.GetBasketballTeamsAsync();

            result.Should().HaveCount(2);
            result.Should().Contain(t => t.School == "Duke");
            result.Should().Contain(t => t.School == "Kansas");
        }

        // ── GetBasketballTeamsAsync — caching ─────────────────────────────────

        [Fact]
        public async Task GetBasketballTeamsAsync_ShouldReturnCachedResult_OnSubsequentCalls()
        {
            var teams = new List<TeamInfo>
            {
                new() { SchoolId = 10, School = "UNC", Mascot = "Tar Heels", Conference = "ACC" }
            };
            var (sut, handler) = Build(HttpStatusCode.OK, SerializeConfig(teams));

            var first = await sut.GetBasketballTeamsAsync();
            var second = await sut.GetBasketballTeamsAsync();
            var third = await sut.GetBasketballTeamsAsync();

            // HTTP endpoint should only have been called once.
            handler.CallCount.Should().Be(1);
            first.Should().BeEquivalentTo(second);
            first.Should().BeEquivalentTo(third);
        }

        [Fact]
        public async Task GetBasketballTeamsAsync_ShouldNotShareCacheWithGetTeamsAsync()
        {
            // Football teams and basketball teams are cached independently.
            var footballTeams = new List<TeamInfo>
            {
                new() { SchoolId = 100, School = "Alabama", Mascot = "Crimson Tide", Conference = "SEC" }
            };
            var basketballTeams = new List<TeamInfo>
            {
                new() { SchoolId = 200, School = "Gonzaga", Mascot = "Bulldogs", Conference = "WCC" }
            };

            // Build a handler that returns basketball data (both endpoints return it for this test —
            // we're only asserting that the caches don't contaminate each other by checking counts).
            var (sut, handler) = Build(HttpStatusCode.OK, SerializeConfig(basketballTeams));

            await sut.GetBasketballTeamsAsync();
            await sut.GetTeamsAsync();

            // Both methods must have made their own HTTP call.
            handler.CallCount.Should().Be(2);
        }

        // ── GetBasketballTeamsAsync — error / null responses ──────────────────

        [Fact]
        public async Task GetBasketballTeamsAsync_ShouldReturnEmptyList_WhenApiReturnsNullConfig()
        {
            var (sut, _) = Build(HttpStatusCode.OK, "null");

            var result = await sut.GetBasketballTeamsAsync();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetBasketballTeamsAsync_ShouldReturnEmptyList_WhenApiReturnsNotFound()
        {
            var (sut, _) = Build(HttpStatusCode.NotFound, string.Empty);

            var result = await sut.GetBasketballTeamsAsync();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetBasketballTeamsAsync_ShouldReturnEmptyList_WhenApiReturnsInvalidJson()
        {
            var (sut, _) = Build(HttpStatusCode.OK, "{ this is not valid json }");

            var result = await sut.GetBasketballTeamsAsync();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetBasketballTeamsAsync_ShouldNotThrow_WhenApiThrows()
        {
            // Simulate a transient network failure by using an unreachable base address and
            // a handler that immediately throws.
            var throwingHandler = new ThrowingHttpMessageHandler();
            var httpClient = new HttpClient(throwingHandler) { BaseAddress = new Uri("https://localhost/") };
            var sut = new ConfigurationService(httpClient);

            var act = () => sut.GetBasketballTeamsAsync();

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task GetBasketballTeamsAsync_ShouldReturnEmptyList_WhenApiThrows()
        {
            var throwingHandler = new ThrowingHttpMessageHandler();
            var httpClient = new HttpClient(throwingHandler) { BaseAddress = new Uri("https://localhost/") };
            var sut = new ConfigurationService(httpClient);

            var result = await sut.GetBasketballTeamsAsync();

            result.Should().BeEmpty();
        }

        // ── GetBasketballTeamsAsync — empty teams list ─────────────────────────

        [Fact]
        public async Task GetBasketballTeamsAsync_ShouldReturnEmptyList_WhenConfigHasNoTeams()
        {
            // A valid TeamConfig document with an empty Teams list should not cache
            // (same behaviour as GetTeamsAsync: cache only kicks in when Any() is true).
            var emptyConfig = new TeamConfig { Teams = new List<TeamInfo>() };
            var body = JsonSerializer.Serialize(emptyConfig);
            var (sut, handler) = Build(HttpStatusCode.OK, body);

            await sut.GetBasketballTeamsAsync();
            await sut.GetBasketballTeamsAsync();

            // Cache was not populated, so the endpoint is called each time.
            handler.CallCount.Should().Be(2);
        }

        // ── Nested helpers ────────────────────────────────────────────────────

        private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken) =>
                throw new HttpRequestException("Simulated network failure");
        }
    }
}
