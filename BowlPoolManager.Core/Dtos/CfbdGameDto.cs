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

        [JsonProperty("notes")]
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        // --- ROOT PROPERTIES (Used by standard /games) ---
        [JsonProperty("homeTeam")]
        [JsonPropertyName("homeTeam")]
        public object? HomeTeamRaw { get; set; }

        [JsonProperty("homePoints")]
        [JsonPropertyName("homePoints")]
        public int? HomePointsRoot { get; set; }

        [JsonProperty("awayTeam")]
        [JsonPropertyName("awayTeam")]
        public object? AwayTeamRaw { get; set; }

        [JsonProperty("awayPoints")]
        [JsonPropertyName("awayPoints")]
        public int? AwayPointsRoot { get; set; }

        // --- SMART WRAPPERS ---
        // These properties detect if the API returned a string or a nested object
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public string? HomeTeam => GetTeamName(HomeTeamRaw);

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public int? HomePoints => GetPoints(HomeTeamRaw) ?? HomePointsRoot;

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public string? AwayTeam => GetTeamName(AwayTeamRaw);

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public int? AwayPoints => GetPoints(AwayTeamRaw) ?? AwayPointsRoot;

        // --- HELPER LOGIC FOR NESTED JSON ---
        private string? GetTeamName(object? raw)
        {
            if (raw == null) return null;
            if (raw is string s) return s;
            
            // Handle nested object from /scoreboard
            var json = raw.ToString();
            if (string.IsNullOrEmpty(json)) return null;
            
            try {
                var nested = JsonConvert.DeserializeObject<CfbdScoreboardTeamDto>(json);
                return nested?.Name;
            } catch { return null; }
        }

        private int? GetPoints(object? raw)
        {
            if (raw == null || raw is string) return null;
            
            var json = raw.ToString();
            if (string.IsNullOrEmpty(json)) return null;

            try {
                var nested = JsonConvert.DeserializeObject<CfbdScoreboardTeamDto>(json);
                return nested?.Points;
            } catch { return null; }
        }
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
