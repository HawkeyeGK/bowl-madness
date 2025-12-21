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

        // --- FLAT PROPERTIES (Used by standard /games endpoint) ---
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

        // --- NESTED PROPERTIES (Used by /scoreboard endpoint) ---
        // We use different internal names to avoid conflicts during deserialization
        [JsonProperty("homeTeamObj")] 
        [JsonPropertyName("homeTeam")] 
        public CfbdScoreboardTeamDto? HomeTeamObj { get; set; }

        [JsonProperty("awayTeamObj")]
        [JsonPropertyName("awayTeam")]
        public CfbdScoreboardTeamDto? AwayTeamObj { get; set; }

        // --- SMART WRAPPERS ---
        // These ensure GameFunctions.cs logic doesn't have to change.
        // It will look for the nested object data first, then fall back to the root property.
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
