using System.Reflection;
using Newtonsoft.Json.Linq;
using BowlPoolManager.Core.ScoreFeed;

namespace BowlPoolManager.Api.Services.ScoreFeed
{
    public interface IMockFeedStartingSet
    {
        /// <summary>A recording of a past season's real postseason games.</summary>
        IReadOnlyList<FeedGame> Load();
    }

    /// <summary>
    /// Reads the recorded starting set embedded in this assembly (MockFeed/StartingSet.json): the raw
    /// College Football Data /games response for one past postseason, saved once as a fixture.
    /// </summary>
    public class EmbeddedMockFeedStartingSet : IMockFeedStartingSet
    {
        private const string Resource = "BowlPoolManager.Api.MockFeed.StartingSet.json";

        public IReadOnlyList<FeedGame> Load()
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(Resource)
                ?? throw new InvalidOperationException("The mock feed starting set is missing.");
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd());
        }

        // CFBD /games fields: id, completed, homeTeam / awayTeam (names), homeId / awayId, homePoints / awayPoints,
        // startDate, notes. Games without an id are skipped.
        public static IReadOnlyList<FeedGame> Parse(string json)
        {
            var games = new List<FeedGame>();
            foreach (var g in JArray.Parse(json))
            {
                var id = (int?)g["id"];
                if (id == null) continue;

                var completed = (bool?)g["completed"] ?? false;
                games.Add(new FeedGame
                {
                    ExternalId = id.Value.ToString(),
                    Status = completed ? FeedGameStatus.Final : FeedGameStatus.Scheduled,
                    StartTime = ParseUtc((string?)g["startDate"]),
                    Home = new FeedTeam { Name = (string?)g["homeTeam"], Id = (int?)g["homeId"], Points = (int?)g["homePoints"] },
                    Away = new FeedTeam { Name = (string?)g["awayTeam"], Id = (int?)g["awayId"], Points = (int?)g["awayPoints"] },
                    Notes = (string?)g["notes"]
                });
            }
            return games;
        }

        private static DateTime? ParseUtc(string? value) =>
            DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var d)
                ? d : null;
    }
}
