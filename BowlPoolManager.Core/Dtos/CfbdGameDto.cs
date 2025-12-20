using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace BowlPoolManager.Core.Dtos
{
    // UPDATED: Now supports both Newtonsoft and System.Text.Json to prevent serialization mismatches
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

        [JsonProperty("season_type")]
        [JsonPropertyName("season_type")]
        public string? SeasonType { get; set; }

        [JsonProperty("start_date")]
        [JsonPropertyName("start_date")]
        public DateTime? StartDate { get; set; }

        [JsonProperty("completed")]
        [JsonPropertyName("completed")]
        public bool Completed { get; set; }

        [JsonProperty("home_team")]
        [JsonPropertyName("home_team")]
        public string? HomeTeam { get; set; }

        [JsonProperty("home_points")]
        [JsonPropertyName("home_points")]
        public int? HomePoints { get; set; }

        [JsonProperty("away_team")]
        [JsonPropertyName("away_team")]
        public string? AwayTeam { get; set; }

        [JsonProperty("away_points")]
        [JsonPropertyName("away_points")]
        public int? AwayPoints { get; set; }

        [JsonProperty("notes")]
        [JsonPropertyName("notes")]
        public string? Notes { get; set; } 
    }
}
