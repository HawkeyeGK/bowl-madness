using FluentAssertions;
using BowlPoolManager.Api.Services.ScoreFeed;
using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Tests.Api
{
    public class MockFeedStartingSetTests
    {
        [Fact]
        public void Load_ShouldReturnTheRecordedGames_WhenFixtureIsEmbedded()
        {
            var games = new EmbeddedMockFeedStartingSet().Load();

            games.Should().HaveCountGreaterThan(50);
            games.Select(g => g.ExternalId).Should().OnlyHaveUniqueItems();
            games.Should().OnlyContain(g => g.Status == FeedGameStatus.Final);
            games.Should().OnlyContain(g => g.Home.Name != null && g.Away.Name != null
                                            && g.Home.Points != null && g.Away.Points != null && g.StartTime != null);
        }

        [Fact]
        public void Load_ShouldHoldNothingButPublicGameData_WhenFixtureIsEmbedded()
        {
            // The recording came from a signed-in session; only game fields may be in it.
            using var stream = typeof(EmbeddedMockFeedStartingSet).Assembly
                .GetManifestResourceStream("BowlPoolManager.Api.MockFeed.StartingSet.json")!;
            var json = new StreamReader(stream).ReadToEnd();
            var allowed = new[] { "id", "startDate", "completed", "homeId", "homeTeam", "homePoints", "awayId", "awayTeam", "awayPoints", "notes" };

            var keys = Newtonsoft.Json.Linq.JArray.Parse(json).SelectMany(g => ((Newtonsoft.Json.Linq.JObject)g).Properties().Select(p => p.Name));

            keys.Distinct().Should().BeSubsetOf(allowed);
            json.Should().NotContain("@");
        }

        [Fact]
        public void Parse_ShouldMapCfbdGamesFields_WhenGivenCfbdJson()
        {
            var json = "[{\"id\":1,\"startDate\":\"2025-12-20T17:00:00.000Z\",\"completed\":true,\"homeId\":5,\"homeTeam\":\"A\",\"homePoints\":3,\"awayId\":6,\"awayTeam\":\"B\",\"awayPoints\":0,\"notes\":\"X Bowl\"},{\"completed\":false}]";

            var games = EmbeddedMockFeedStartingSet.Parse(json);

            var g = games.Should().ContainSingle().Subject; // the game without an id is skipped
            g.ExternalId.Should().Be("1");
            g.Status.Should().Be(FeedGameStatus.Final);
            g.Home.Id.Should().Be(5);
            g.Home.Points.Should().Be(3);
            g.Away.Name.Should().Be("B");
            g.Notes.Should().Be("X Bowl");
            g.StartTime.Should().Be(new DateTime(2025, 12, 20, 17, 0, 0, DateTimeKind.Utc));
        }
    }
}
