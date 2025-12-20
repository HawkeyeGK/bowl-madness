using System.Text.Json.Serialization;

namespace BowlPoolManager.Core.Dtos
{
    // Minimal DTO for fetching game data from CFBD
    public class CfbdGameDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("season")]
        public int Season { get; set; }

        [JsonPropertyName("week")]
        public int Week { get; set; }

        [JsonPropertyName("season_type")]
        public string? SeasonType { get; set; }

        [JsonPropertyName("start_date")]
        public DateTime? StartDate { get; set; }

        [JsonPropertyName("completed")]
        public bool Completed { get; set; }

        [JsonPropertyName("home_team")]
        public string? HomeTeam { get; set; }

        [JsonPropertyName("home_points")]
        public int? HomePoints { get; set; }

        [JsonPropertyName("away_team")]
        public string? AwayTeam { get; set; }

        [JsonPropertyName("away_points")]
        public int? AwayPoints { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; } 
    }
}
