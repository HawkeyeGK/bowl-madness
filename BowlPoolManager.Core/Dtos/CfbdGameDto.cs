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

        // Preserved to maintain compatibility with GameLinker.razor
        [JsonProperty("notes")]
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        // --- FLAT DATA (Used by /games) ---
        [JsonProperty("homeTeam")]
        [JsonPropertyName("homeTeam")]
        public object? HomeRaw { get; set; } // Capture as object to detect string vs nested

        [JsonProperty("homePoints")]
        [JsonPropertyName("homePoints")]
        public int? HomePointsRoot { get; set; }

        [JsonProperty("awayTeam")]
        [JsonPropertyName("awayTeam")]
        public object? AwayRaw { get; set; }

        [JsonProperty("awayPoints")]
        [JsonPropertyName("awayPoints")]
        public int? AwayPointsRoot { get; set; }

        // --- SMART PROPERTIES (Used by GameFunctions.cs) ---
        // These wrappers detect the format and return the correct value
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

        // --- HELPER LOGIC ---
        private string? GetName(object? raw)
        {
            if (raw == null) return null;
            if (raw is string s) return s;
            
            // If it's a nested object (Scoreboard format)
            try {
                var nested = JsonConvert.DeserializeObject<CfbdNestedTeam>(raw.ToString());
                return nested?.Name;
            } catch { return null; }
        }

        private int? GetPoints(object? raw)
        {
            if (raw == null || raw is string) return null;
            
            try {
                var nested = JsonConvert.DeserializeObject<CfbdNestedTeam>(raw.ToString());
                return nested?.Points;
            } catch { return null; }
        }
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
