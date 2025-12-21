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

        // --- ROOT RAW DATA ---
        // We use 'object' to handle either a string (from /games) or an object (from /scoreboard)
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

        // --- NESTED DESERIALIZATION LOGIC ---
        private string? GetName(object? raw)
        {
            if (raw == null) return null;
            if (raw is string s) return s;
            
            try {
                var nested = JsonConvert.DeserializeObject<CfbdNestedTeam>(raw.ToString()!);
                return nested?.Name;
            } catch { return null; }
        }

        private int? GetPoints(object? raw)
        {
            if (raw == null || raw is string) return null;
            
            try {
                var nested = JsonConvert.DeserializeObject<CfbdNestedTeam>(raw.ToString()!);
                return nested?.Points;
            } catch { return null; }
        }

        // Notes is required for the Linker UI
        [JsonProperty("notes")]
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }

    public class CfbdNestedTeam
    {
        [JsonProperty("name")]
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonProperty("points")]
        [JsonPropertyName("points")]
        public int? Points { get; set; }
    }
}
