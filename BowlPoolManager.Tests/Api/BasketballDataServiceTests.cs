using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// Stub HttpMessageHandler that always returns a pre-set response.
    /// Used in place of a live network call for BasketballDataService tests.
    /// </summary>
    internal sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public StubHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    public class BasketballDataServiceTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────────

        private static BasketballDataService BuildService(HttpStatusCode statusCode, string responseBody)
        {
            var handler = new StubHttpMessageHandler(statusCode, responseBody);
            var httpClient = new HttpClient(handler);
            var logger = NullLogger<BasketballDataService>.Instance;
            return new BasketballDataService(httpClient, logger);
        }

        /// <summary>
        /// Sets the required env var for the duration of the test, then restores the original.
        /// </summary>
        private static IDisposable WithApiKey(string value) =>
            new EnvVarScope("CfbdApiKeyHoops", value);

        private sealed class EnvVarScope : IDisposable
        {
            private readonly string _key;
            private readonly string? _previous;

            public EnvVarScope(string key, string value)
            {
                _key = key;
                _previous = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, value);
            }

            public void Dispose() =>
                Environment.SetEnvironmentVariable(_key, _previous);
        }

        // ── GetTeamsAsync — happy path ────────────────────────────────────────

        [Fact]
        public async Task GetTeamsAsync_ShouldReturnMappedTeams_WhenApiReturnsValidJson()
        {
            var json = """
                [
                  {
                    "id": 101,
                    "school": "Duke",
                    "mascot": "Blue Devils",
                    "abbreviation": "DUKE",
                    "conference": "ACC",
                    "primaryColor": "#003087",
                    "secondaryColor": "#FFFFFF"
                  },
                  {
                    "id": 202,
                    "school": "Kansas",
                    "mascot": "Jayhawks",
                    "abbreviation": "KU",
                    "conference": "Big 12",
                    "primaryColor": "#0051A5",
                    "secondaryColor": "#E8000D"
                  }
                ]
                """;

            using var _ = WithApiKey("test-key");
            var sut = BuildService(HttpStatusCode.OK, json);

            var result = await sut.GetTeamsAsync();

            result.Should().HaveCount(2);

            var duke = result.First(t => t.School == "Duke");
            duke.SchoolId.Should().Be(101);
            duke.Mascot.Should().Be("Blue Devils");
            duke.Abbreviation.Should().Be("DUKE");
            duke.Conference.Should().Be("ACC");
            duke.Color.Should().Be("#003087");
            duke.AltColor.Should().Be("#FFFFFF");
        }

        [Fact]
        public async Task GetTeamsAsync_ShouldSetLogosToEmpty_WhenApiReturnsNoLogos()
        {
            // When the API response omits the logos field, Logos should be an empty collection (not null).
            var json = """
                [
                  { "id": 1, "school": "UNC", "mascot": "Tar Heels",
                    "abbreviation": "UNC", "conference": "ACC",
                    "primaryColor": "#7BAFD4", "secondaryColor": "#FFFFFF" }
                ]
                """;

            using var _ = WithApiKey("test-key");
            var sut = BuildService(HttpStatusCode.OK, json);

            var result = await sut.GetTeamsAsync();

            result.Should().HaveCount(1);
            result[0].Logos.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public async Task GetTeamsAsync_ShouldMapLogos_WhenApiReturnsLogoUrls()
        {
            // When the API response includes logo URLs, they should be mapped into the Logos list.
            var json = """
                [
                  { "id": 1, "school": "Duke", "mascot": "Blue Devils",
                    "abbreviation": "DUKE", "conference": "ACC",
                    "primaryColor": "#003087", "secondaryColor": "#FFFFFF",
                    "logos": ["https://example.com/duke.png", "https://example.com/duke-dark.png"] }
                ]
                """;

            using var _ = WithApiKey("test-key");
            var sut = BuildService(HttpStatusCode.OK, json);

            var result = await sut.GetTeamsAsync();

            result.Should().HaveCount(1);
            result[0].Logos.Should().HaveCount(2)
                .And.Contain("https://example.com/duke.png")
                .And.Contain("https://example.com/duke-dark.png");
        }

        [Fact]
        public async Task GetTeamsAsync_ShouldUseEmptyString_WhenOptionalFieldsAreNull()
        {
            // All nullable string fields on the DTO should coalesce to string.Empty.
            var json = """
                [{ "id": 9 }]
                """;

            using var _ = WithApiKey("test-key");
            var sut = BuildService(HttpStatusCode.OK, json);

            var result = await sut.GetTeamsAsync();

            result.Should().HaveCount(1);
            result[0].School.Should().BeEmpty();
            result[0].Mascot.Should().BeEmpty();
            result[0].Abbreviation.Should().BeEmpty();
            result[0].Conference.Should().BeEmpty();
            result[0].Color.Should().BeEmpty();
            result[0].AltColor.Should().BeEmpty();
        }

        // ── GetTeamsAsync — missing API key ───────────────────────────────────

        [Fact]
        public async Task GetTeamsAsync_ShouldReturnEmptyList_WhenApiKeyEnvVarIsMissing()
        {
            // Ensure the env var is absent for this test.
            using var _ = WithApiKey(string.Empty);
            var sut = BuildService(HttpStatusCode.OK, "[]");

            var result = await sut.GetTeamsAsync();

            result.Should().BeEmpty();
        }

        // ── GetTeamsAsync — non-success HTTP status ───────────────────────────

        [Fact]
        public async Task GetTeamsAsync_ShouldReturnEmptyList_WhenApiReturnsNonSuccessStatus()
        {
            using var _ = WithApiKey("test-key");
            var sut = BuildService(HttpStatusCode.Unauthorized, "Unauthorized");

            var result = await sut.GetTeamsAsync();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetTeamsAsync_ShouldReturnEmptyList_WhenApiReturns500()
        {
            using var _ = WithApiKey("test-key");
            var sut = BuildService(HttpStatusCode.InternalServerError, "Server Error");

            var result = await sut.GetTeamsAsync();

            result.Should().BeEmpty();
        }

        // ── GetTeamsAsync — malformed / empty JSON ─────────────────────────────

        [Fact]
        public async Task GetTeamsAsync_ShouldReturnEmptyList_WhenResponseIsEmptyArray()
        {
            using var _ = WithApiKey("test-key");
            var sut = BuildService(HttpStatusCode.OK, "[]");

            var result = await sut.GetTeamsAsync();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetTeamsAsync_ShouldReturnEmptyList_WhenResponseBodyIsEmptyString()
        {
            using var _ = WithApiKey("test-key");
            var sut = BuildService(HttpStatusCode.OK, string.Empty);

            var result = await sut.GetTeamsAsync();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetTeamsAsync_ShouldReturnEmptyList_WhenResponseBodyIsInvalidJson()
        {
            using var _ = WithApiKey("test-key");
            var sut = BuildService(HttpStatusCode.OK, "not valid json {{");

            var result = await sut.GetTeamsAsync();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetTeamsAsync_ShouldReturnEmptyList_WhenResponseBodyIsNull_Literal()
        {
            // API returning JSON "null" should not throw and should return empty list.
            using var _ = WithApiKey("test-key");
            var sut = BuildService(HttpStatusCode.OK, "null");

            var result = await sut.GetTeamsAsync();

            result.Should().BeEmpty();
        }
    }
}
