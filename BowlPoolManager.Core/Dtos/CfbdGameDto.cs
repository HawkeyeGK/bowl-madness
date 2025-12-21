using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json;

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

        // --- NEW FIELDS FOR LIVE STATUS ---
        [JsonProperty("status")]
        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string? StatusRaw { get; set; } // "scheduled", "in_progress", "final"

        [JsonProperty("period")]
        [System.Text.Json.Serialization.JsonPropertyName("period")]
        public int? Period { get; set; }

        [JsonProperty("clock")]
        [System.Text.Json.Serialization.JsonPropertyName("clock")]
        public string? Clock { get; set; } 
        // ----------------------------------

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
        public string? HomeTeam => GetValue(HomeRaw, "name");

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public int? HomePoints => GetInt(HomeRaw, "points") ?? HomePointsRoot;

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public string? AwayTeam => GetValue(AwayRaw, "name");

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public int? AwayPoints => GetInt(AwayRaw, "points") ?? AwayPointsRoot;

        // --- BILINGUAL EXTRACTION LOGIC ---
        private string? GetValue(object? raw, string key)
        {
            if (raw == null) return null;
            
            // 1. Simple String (Common in /games endpoint)
            if (raw is string s) return s;
            
            // 2. Server-Side (Newtonsoft JObject)
            if (raw is JObject jo) return jo[key]?.ToString();

            // 3. Client-Side (System.Text.Json JsonElement)
            if (raw is JsonElement je)
            {
                // If the element itself is a string (e.g. /games endpoint processed by STJ)
                if (je.ValueKind == JsonValueKind.String) 
                    return je.GetString();

                // If the element is an object (e.g. /scoreboard endpoint)
                if (je.ValueKind == JsonValueKind.Object && je.TryGetProperty(key, out var prop))
                {
                     if (prop.ValueKind == JsonValueKind.String) return prop.GetString();
                     return prop.ToString();
                }
            }

            return null;
        }

        private int? GetInt(object? raw, string key)
        {
            // 1. Server-Side (Newtonsoft)
            if (raw is JObject jo) return jo[key]?.Value<int?>();

            // 2. Client-Side (System.Text.Json)
            if (raw is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.Object && 
                    je.TryGetProperty(key, out var prop) && 
                    prop.TryGetInt32(out int val))
                {
                    return val;
                }
            }
            return null;
        }

        [JsonProperty("notes")]
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }
}
