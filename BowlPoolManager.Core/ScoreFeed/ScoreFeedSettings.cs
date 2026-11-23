using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace BowlPoolManager.Core.ScoreFeed
{
    public enum ScoreFeedMode
    {
        Live,
        Mock
    }

    /// <summary>
    /// The one document that says which score feed is in use. Stored in the Configuration container
    /// (id and partition key are both "Config_ScoreFeed"). A missing document means Live.
    /// </summary>
    public class ScoreFeedSettings
    {
        public const string DocumentId = "Config_ScoreFeed";
        public const string DocumentType = "ScoreFeedSettings";

        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = DocumentId;

        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public string Type { get; set; } = DocumentType;

        [JsonProperty("mode")]
        [JsonPropertyName("mode")]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        [System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumConverter))]
        public ScoreFeedMode Mode { get; set; } = ScoreFeedMode.Live;

        [JsonProperty("updatedUtc")]
        [JsonPropertyName("updatedUtc")]
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>What a mock reset would do (preview) or did (apply).</summary>
    public class MockFeedResetSummary
    {
        // Linked games of the current season that have a result and go back to Scheduled with no score.
        [JsonProperty("gamesToReset")]
        [JsonPropertyName("gamesToReset")]
        public int GamesToReset { get; set; }

        // Names of those games, so the admin can see exactly what is cleared (real and hand-entered results too).
        [JsonProperty("gameNames")]
        [JsonPropertyName("gameNames")]
        public List<string> GameNames { get; set; } = new();

        // Later-round slots that return to "Winner of ...".
        [JsonProperty("slotsToClear")]
        [JsonPropertyName("slotsToClear")]
        public int SlotsToClear { get; set; }

        // Slots with a result entered another way; left unchanged, for the admin to check.
        [JsonProperty("needsReview")]
        [JsonPropertyName("needsReview")]
        public List<string> NeedsReview { get; set; } = new();

        // Slots that do not hold the feeder's winner (edited by hand); left unchanged.
        [JsonProperty("leftAlone")]
        [JsonPropertyName("leftAlone")]
        public List<string> LeftAlone { get; set; } = new();
    }
}
