using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace BowlPoolManager.Core.Dtos
{
    public class CfbdGameDto
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("completed")]
        [JsonPropertyName("completed")]
        public bool Completed { get; set; }

        // --- ROOT PROPERTIES (Used by /games) ---
        [JsonProperty("homeTeam")]
        [JsonPropertyName("homeTeam")]
        public string? HomeTeamRoot { get; set; }

        [JsonProperty("homePoints")]
        [JsonPropertyName("homePoints")]
        public int? HomePointsRoot { get; set; }

        [JsonProperty("awayTeam")]
        [JsonPropertyName("awayTeam")]
        public string? AwayTeamRoot { get; set; }

        [JsonProperty("awayPoints")]
        [JsonPropertyName("awayPoints")]
        public int? AwayPointsRoot { get; set; }

        // --- NESTED PROPERTIES (Used by /scoreboard) ---
        [JsonProperty("homeTeamObj")] // Map 'homeTeam' object from scoreboard
        [JsonPropertyName("homeTeam")] 
        public CfbdScoreboardTeamDto? HomeTeamObj { get; set; }

        [JsonProperty("awayTeamObj")] // Map 'awayTeam' object from scoreboard
        [JsonPropertyName("awayTeam")]
        public CfbdScoreboardTeamDto? AwayTeamObj { get; set; }

        // --- SMART WRAPPERS ---
        // These properties ensure GameFunctions logic works for BOTH API types
        [JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public string? HomeTeam => HomeTeamObj?.Name ?? HomeTeamRoot;

        [JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public int? HomePoints => HomeTeamObj?.Points ?? HomePointsRoot;

        [JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public string? AwayTeam => AwayTeamObj?.Name ?? AwayTeamRoot;

        [JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public int? AwayPoints => AwayTeamObj?.Points ?? AwayPointsRoot;
    }

    public class CfbdScoreboardTeamDto
    {
        [JsonProperty("name")]
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonProperty("points")]
        [JsonPropertyName("points")]
        public int? Points { get; set; }
    }
}
