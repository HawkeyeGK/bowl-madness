using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace BowlPoolManager.Core.Dtos
{
    public class CfbdGameDto
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("season")]
        [JsonPropertyName("season")]
        public int Season { get; set; }

        [JsonProperty("week")]
        [JsonPropertyName("week")]
        public int Week { get; set; }

        // FIXED: Updated to camelCase to match API response
        [JsonProperty("seasonType")] 
        [JsonPropertyName("seasonType")]
        public string? SeasonType { get; set; }

        [JsonProperty("startDate")]
        [JsonPropertyName("startDate")]
        public DateTime? StartDate { get; set; }

        [JsonProperty("completed")]
        [JsonPropertyName("completed")]
        public bool Completed { get; set; }

        // THE FIX: "homeTeam" (camelCase)
        [JsonProperty("homeTeam")]
        [JsonPropertyName("homeTeam")]
        public string? HomeTeam { get; set; }

        [JsonProperty("homePoints")]
        [JsonPropertyName("homePoints")]
        public int? HomePoints { get; set; }

        [JsonProperty("awayTeam")]
        [JsonPropertyName("awayTeam")]
        public string? AwayTeam { get; set; }

        [JsonProperty("awayPoints")]
        [JsonPropertyName("awayPoints")]
        public int? AwayPoints { get; set; }

        [JsonProperty("notes")]
        [JsonPropertyName("notes")]
        public string? Notes { get; set; } 
    }
}
