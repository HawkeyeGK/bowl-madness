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

        // --- TEAM ID EXTRACTION ---
        // Direct Mapping for Root Level IDs (Games Endpoint)
        [JsonProperty("home_id")]
        [JsonPropertyName("home_id")]
        public int? HomeIdRaw { get; set; }

        [JsonProperty("away_id")]
        [JsonPropertyName("away_id")]
        public int? AwayIdRaw { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public int? HomeId 
        {
            get
            {
                // Source 1: "home_id" at root (Games Endpoint)
                if (HomeIdRaw.HasValue) return HomeIdRaw.Value;

                // Source 2: "id" inside homeTeam object (Scoreboard Endpoint)
                return GetInt(HomeRaw, "id");
            }
        }

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public int? AwayId 
        {
            get
            {
                // Source 1: "away_id" at root (Games Endpoint)
                if (AwayIdRaw.HasValue) return AwayIdRaw.Value;

                // Source 2: "id" inside awayTeam object (Scoreboard Endpoint)
                 return GetInt(AwayRaw, "id");
            }
        }

        // --- BILINGUAL EXTRACTION LOGIC ---
        private string? GetValue(object? raw, string key)
        {
            if (raw == null) return null;
            
            // 1. Simple String (Common in /games endpoint)
            if (raw is string s) return s;

            // 2. Dictionary (Fix for CfbdService manual mapping)
            if (raw is IDictionary<string, object> dict)
            {
                return dict.TryGetValue(key, out var val) ? val?.ToString() : null;
            }
            
            // 3. Server-Side (Newtonsoft JObject)
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
            if (raw == null) return null;

            // SPECIAL CASE: Checking "this" for root properties (reflection/dynamic check not ideal but keeping consistent with pattern)
            // However, 'this' is CfbdGameDto, not a dynamic object. 
            // The helper 'GetInt' expects dynamic/dictionary/json objects. 
            // Validating if 'raw' is the DTO itself is tricky without reflection.
            // BETTER APPROACH for Root properties: use the JSON extension data or similar if we had it.
            // BUT, since we are using 'raw' passed from the property which might be 'this'?? 
            // No, I can't pass 'this' to GetInt easily if GetInt expects specific types.
            // Let's rely on standard JObject/Dictionary parsing logic.
            
            // 1. Dictionary (Fix for CfbdService manual mapping)
            if (raw is IDictionary<string, object> dict)
            {
                if (dict.TryGetValue(key, out var val) && val != null)
                {
                    // Handle Int64 (long) which Newtonsoft often produces for integers in object/dynamic contexts
                    try { return Convert.ToInt32(val); } catch { return null; }
                }
                return null;
            }

            // 2. Server-Side (Newtonsoft)
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
