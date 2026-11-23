using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace BowlPoolManager.Core.ScoreFeed
{
    public enum FeedGameStatus
    {
        Scheduled,
        InProgress,
        Final
    }

    public class FeedTeam
    {
        // The feed's own spelling. Matched to BowlGame.ApiHomeTeam / ApiAwayTeam.
        [JsonProperty("name")]
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        // Null until the feed reports a score.
        [JsonProperty("points")]
        [JsonPropertyName("points")]
        public int? Points { get; set; }
    }

    /// <summary>One game as a score feed reports it, whichever provider it came from.</summary>
    public class FeedGame
    {
        // A string because BowlGame.ExternalId is one; CFBD and ESPN ids are integers.
        [JsonProperty("externalId")]
        [JsonPropertyName("externalId")]
        public string ExternalId { get; set; } = string.Empty;

        [JsonProperty("status")]
        [JsonPropertyName("status")]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        [System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumConverter))]
        public FeedGameStatus Status { get; set; } = FeedGameStatus.Scheduled;

        // Football: 1-4, 5+ is overtime. Hoops: 1-2, 3+ is overtime. Labels stay in the sport's scoring service.
        [JsonProperty("period")]
        [JsonPropertyName("period")]
        public int? Period { get; set; }

        [JsonProperty("clock")]
        [JsonPropertyName("clock")]
        public string? Clock { get; set; }

        // UTC. Null when the provider's payload carries no start time (the CFBD DTO does not).
        [JsonProperty("startTime")]
        [JsonPropertyName("startTime")]
        public DateTime? StartTime { get; set; }

        [JsonProperty("home")]
        [JsonPropertyName("home")]
        public FeedTeam Home { get; set; } = new();

        [JsonProperty("away")]
        [JsonPropertyName("away")]
        public FeedTeam Away { get; set; } = new();

        // Bowl name or headline; used by the game linker.
        [JsonProperty("notes")]
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// A feed game stored by the mock feed (test site only). Lives in the Seasons container under the fixed
    /// partition value "MockFeed", so season queries never see it and ResetTestData removes it.
    /// </summary>
    public class MockFeedGame
    {
        public const string PartitionValue = "MockFeed";
        public const string DocumentType = "MockFeedGame";

        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty; // same as Game.ExternalId

        [JsonProperty("seasonId")]
        [JsonPropertyName("seasonId")]
        public string SeasonId { get; set; } = PartitionValue;

        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public string Type { get; set; } = DocumentType;

        [JsonProperty("game")]
        [JsonPropertyName("game")]
        public FeedGame Game { get; set; } = new();
    }
}
