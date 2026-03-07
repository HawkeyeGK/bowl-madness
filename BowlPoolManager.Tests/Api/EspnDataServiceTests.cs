using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using BowlPoolManager.Api.Services;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// Stub HttpMessageHandler for EspnDataService tests.
    /// Returns a fixed status code and body for every request.
    /// </summary>
    internal sealed class EspnStubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public EspnStubHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
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

    public class EspnDataServiceTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────────

        private static EspnDataService BuildService(HttpStatusCode statusCode, string responseBody)
        {
            var handler = new EspnStubHttpMessageHandler(statusCode, responseBody);
            var httpClient = new HttpClient(handler);
            var logger = NullLogger<EspnDataService>.Instance;
            return new EspnDataService(httpClient, logger);
        }

        /// <summary>
        /// Builds a minimal valid ESPN API JSON response containing the provided team entries.
        /// Each entry is a JSON object placed inside the teams array as { "team": { ... } }.
        /// </summary>
        private static string BuildEspnJson(params string[] teamEntries)
        {
            var entries = string.Join(",", teamEntries);
            return $$"""
                {
                  "sports": [{
                    "leagues": [{
                      "teams": [{{entries}}]
                    }]
                  }]
                }
                """;
        }

        private static string TeamEntry(
            string id,
            string location,
            string name,
            string abbreviation = "ABB",
            string color = "000000",
            string altColor = "ffffff",
            string[]? logoHrefs = null)
        {
            var logosJson = logoHrefs == null || logoHrefs.Length == 0
                ? "[]"
                : "[" + string.Join(",", logoHrefs.Select(h => $$"""{"href":"{{h}}"}""")) + "]";

            return $$"""
                {
                  "team": {
                    "id": "{{id}}",
                    "location": "{{location}}",
                    "name": "{{name}}",
                    "abbreviation": "{{abbreviation}}",
                    "color": "{{color}}",
                    "alternateColor": "{{altColor}}",
                    "logos": {{logosJson}}
                  }
                }
                """;
        }

        // ── GetTeamsAsync — happy path ────────────────────────────────────────

        [Fact]
        public async Task GetTeamsAsync_ShouldReturnMappedTeams_WhenApiReturnsValidJson()
        {
            var json = BuildEspnJson(
                TeamEntry("101", "Duke", "Blue Devils", "DUKE", "003087", "ffffff",
                    new[] { "https://a.espncdn.com/duke.png" }),
                TeamEntry("202", "Kansas", "Jayhawks", "KU", "0051a5", "e8000d",
                    new[] { "https://a.espncdn.com/kansas.png" })
            );

            var sut = BuildService(HttpStatusCode.OK, json);

            var result = await sut.GetTeamsAsync();

            result.Should().HaveCount(2);

            var duke = result.First(t => t.School == "Duke");
            duke.SchoolId.Should().Be(101);
            duke.Mascot.Should().Be("Blue Devils");
            duke.Abbreviation.Should().Be("DUKE");
            duke.Color.Should().Be("003087");
            duke.AltColor.Should().Be("ffffff");
        }

        [Fact]
        public async Task GetTeamsAsync_ShouldMapLogos_WhenApiProvidesMultipleLogoHrefs()
        {
            var json = BuildEspnJson(
                TeamEntry("1", "Duke", "Blue Devils", logoHrefs: new[]
                {
                    "https://a.espncdn.com/duke.png",
                    "https://a.espncdn.com/duke-dark.png"
                })
            );

            var sut = BuildService(HttpStatusCode.OK, json);

            var result = await sut.GetTeamsAsync();

            result.Should().HaveCount(1);
            result[0].Logos.Should().HaveCount(2)
                .And.Contain("https://a.espncdn.com/duke.png")
                .And.Contain("https://a.espncdn.com/duke-dark.png");
        }

        [Fact]
        public async Task GetTeamsAsync_ShouldSetLogosToEmpty_WhenApiProvidesEmptyLogosArray()
        {
            var json = BuildEspnJson(
                TeamEntry("5", "UNC", "Tar Heels", logoHrefs: Array.Empty<string>())
            );

            var sut = BuildService(HttpStatusCode.OK, json);

            var result = await sut.GetTeamsAsync();

            result.Should().HaveCount(1);
            result[0].Logos.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public async Task GetTeamsAsync_ShouldParseSchoolIdAsInt_WhenIdFieldIsNumericString()
        {
            var json = BuildEspnJson(TeamEntry("99", "Villanova", "Wildcats"));

            var sut = BuildService(HttpStatusCode.OK, json);

            var result = await sut.GetTeamsAsync();

            result.Should().HaveCount(1);
            result[0].SchoolId.Should().Be(99);
        }

        [Fact]
        public async Task GetTeamsAsync_ShouldSetSchoolIdToZero_WhenIdFieldIsNonNumeric()
        {
            // ESPN returns a non-parseable id — SchoolId should default to 0, not throw.
            var json = """
                {
                  "sports": [{
                    "leagues": [{
                      "teams": [{
                        "team": {
                          "id": "not-a-number",
                          "location": "SomeSchool",
                          "name": "Mascot",
                          "abbreviation": "SS",
                          "color": "000000",
                          "alternateColor": "ffffff",
                          "logos": []
                        }
                      }]
                    }]
                  }]
                }
                """;

            var sut = BuildService(HttpStatusCode.OK, json);

            var result = await sut.GetTeamsAsync();

            result.Should().HaveCount(1);
            result[0].SchoolId.Should().Be(0);
        }

        [Fact]
        public async Task GetTeamsAsync_ShouldUseEmptyString_WhenOptionalFieldsAreMissing()
        {
            // Only the "id" field is present; all string fields should coalesce to string.Empty.
            var json = """
                {
                  "sports": [{
                    "leagues": [{
                      "teams": [{ "team": { "id": "7" } }]
                    }]
                  }]
                }
                """;

            var sut = BuildService(HttpStatusCode.OK, json);

            var result = await sut.GetTeamsAsync();

            result.Should().HaveCount(1);
            result[0].School.Should().BeEmpty();
            result[0].Mascot.Should().BeEmpty();
            result[0].Abbreviation.Should().BeEmpty();
            result[0].Color.Should().BeEmpty();
            result[0].AltColor.Should().BeEmpty();
        }

        [Fact]
        public async Task GetTeamsAsync_ShouldSkipEntries_WhenTeamNodeIsAbsent()
        {
            // An entry in the teams array with no "team" property should be silently skipped.
            var json = """
                {
                  "sports": [{
                    "leagues": [{
                      "teams": [
                        { "team": { "id": "1", "location": "Duke", "name": "Blue Devils", "logos": [] } },
                        { "notATeam": {} }
                      ]
                    }]
                  }]
                }
                """;

            var sut = BuildService(HttpStatusCode.OK, json);

            var result = await sut.GetTeamsAsync();

            result.Should().HaveCount(1);
            result[0].School.Should().Be("Duke");
        }

        [Fact]
        public async Task GetTeamsAsync_ShouldExcludeNullHrefs_WhenLogosArrayContainsNullHref()
        {
            // Logos entries with a null "href" value should be filtered out.
            var json = """
                {
                  "sports": [{
                    "leagues": [{
                      "teams": [{
                        "team": {
                          "id": "10",
                          "location": "Duke",
                          "name": "Blue Devils",
                          "logos": [
                            { "href": "https://a.espncdn.com/duke.png" },
                            { "href": null }
                          ]
                        }
                      }]
                    }]
                  }]
                }
                """;

            var sut = BuildService(HttpStatusCode.OK, json);

            var result = await sut.GetTeamsAsync();

            result.Should().HaveCount(1);
            result[0].Logos.Should().HaveCount(1)
                .And.Contain("https://a.espncdn.com/duke.png");
        }

        // ── GetTeamsAsync — missing / malformed JSON structure ────────────────

        [Fact]
        public async Task GetTeamsAsync_ShouldReturnEmptyList_WhenSportsPathIsMissing()
        {
            // A valid JSON object that does not contain "sports" should return empty, not throw.
            var json = """{ "something": "else" }""";

            var sut = BuildService(HttpStatusCode.OK, json);

            var result = await sut.GetTeamsAsync();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetTeamsAsync_ShouldReturnEmptyList_WhenTeamsArrayIsEmpty()
        {
            var json = BuildEspnJson(); // no entries

            var sut = BuildService(HttpStatusCode.OK, json);

            var result = await sut.GetTeamsAsync();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetTeamsAsync_ShouldReturnEmptyList_WhenResponseBodyIsInvalidJson()
        {
            var sut = BuildService(HttpStatusCode.OK, "this is not { json }");

            var result = await sut.GetTeamsAsync();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetTeamsAsync_ShouldReturnEmptyList_WhenResponseBodyIsEmptyString()
        {
            var sut = BuildService(HttpStatusCode.OK, string.Empty);

            var result = await sut.GetTeamsAsync();

            result.Should().BeEmpty();
        }

        // ── GetTeamsAsync — non-success HTTP status ───────────────────────────

        [Fact]
        public async Task GetTeamsAsync_ShouldReturnEmptyList_WhenApiReturnsNonSuccessStatus()
        {
            // HttpClient.GetStringAsync throws on non-2xx; the service should catch and return empty.
            var sut = BuildService(HttpStatusCode.Unauthorized, "Unauthorized");

            var result = await sut.GetTeamsAsync();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetTeamsAsync_ShouldReturnEmptyList_WhenApiReturns500()
        {
            var sut = BuildService(HttpStatusCode.InternalServerError, "Server Error");

            var result = await sut.GetTeamsAsync();

            result.Should().BeEmpty();
        }

        // ── GetTeamsAsync — multiple teams ordering ───────────────────────────

        [Fact]
        public async Task GetTeamsAsync_ShouldReturnAllTeams_WhenApiReturnsLargeList()
        {
            var entries = Enumerable.Range(1, 10)
                .Select(i => TeamEntry(i.ToString(), $"School{i}", $"Mascot{i}"))
                .ToArray();
            var json = BuildEspnJson(entries);

            var sut = BuildService(HttpStatusCode.OK, json);

            var result = await sut.GetTeamsAsync();

            result.Should().HaveCount(10);
            result.Select(t => t.SchoolId).Should().BeEquivalentTo(Enumerable.Range(1, 10));
        }
    }
}
