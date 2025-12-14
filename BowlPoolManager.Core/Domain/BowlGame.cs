using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace BowlPoolManager.Core.Domain
{
    public class BowlGame
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("bowlName")]
        [JsonPropertyName("bowlName")]
        public string BowlName { get; set; } = string.Empty;

        [JsonProperty("startTime")]
        [JsonPropertyName("startTime")]
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        // TEAMS
        [JsonProperty("teamHome")]
        [JsonPropertyName("teamHome")]
        public string TeamHome { get; set; } = string.Empty;

        [JsonProperty("teamAway")]
        [JsonPropertyName("teamAway")]
        public string TeamAway { get; set; } = string.Empty;

        // SEEDS
        [JsonProperty("teamHomeSeed")]
        [JsonPropertyName("teamHomeSeed")]
        public int? TeamHomeSeed { get; set; }

        [JsonProperty("teamAwaySeed")]
        [JsonPropertyName("teamAwaySeed")]
        public int? TeamAwaySeed { get; set; }

        // SCORING
        [JsonProperty("pointValue")]
        [JsonPropertyName("pointValue")]
        public int PointValue { get; set; } = 1;

        // PLAYOFF LOGIC
        [JsonProperty("isPlayoff")]
        [JsonPropertyName("isPlayoff")]
        public bool IsPlayoff { get; set; } = false;

        // 0 = Standard, 1 = Rd 1, 2 = QF, 3 = SF, 4 = Championship
        [JsonProperty("round")]
        [JsonPropertyName("round")]
        public int Round { get; set; } = 0;

        // BRACKET LINKAGE
        [JsonProperty("nextGameId")]
        [JsonPropertyName("nextGameId")]
        public string? NextGameId { get; set; }

        // "Home" or "Away"
        [JsonProperty("nextGameSlot")]
        [JsonPropertyName("nextGameSlot")]
        public string? NextGameSlot { get; set; }

        // COSMOS DISCRIMINATOR
        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public string Type { get; set; } = "BowlGame";
    }
}
