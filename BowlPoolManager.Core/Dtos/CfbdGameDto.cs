using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

        // --- ROOT RAW DATA ---
        [JsonProperty("homeTeam")]
        [JsonPropertyName("homeTeam")]
        public object? HomeRaw { get; set; }

        [JsonProperty("homePoints")]
        [JsonPropertyName("homePoints")]
        public int? HomePointsRoot { get; set; }

        [JsonProperty("awayTeam")]
        [JsonPropertyName("awayTeam")]
        public object? AwayRaw { get; set; }

        [JsonProperty("awayPoints")]
        [JsonPropertyName("awayPoints")]
        public int? AwayPointsRoot { get; set; }

        // --- SMART WRAPPERS for GameFunctions.cs ---
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public string? HomeTeam => GetName(HomeRaw);

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public int? HomePoints => GetPoints(HomeRaw) ?? HomePointsRoot;

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public string? AwayTeam => GetName(AwayRaw);

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public int? AwayPoints => GetPoints(AwayRaw) ?? AwayPointsRoot;

        private string? GetName(object? raw)
        {
            if (raw == null) return null;
            if (raw is string s) return s;
            
            // Handle Scoreboard API objects
            if (raw is JObject jo)
            {
                return jo["name"]?.ToString();
            }

            return null;
        }

        private int? GetPoints(object? raw)
        {
            if (raw == null) return null;
            
            // Handle Scoreboard API objects
            if (raw is JObject jo)
            {
                return jo["points"]?.Value<int?>();
            }

            return null;
        }

        [JsonProperty("notes")]
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }
}
