using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace BowlPoolManager.Core.Dtos
{
    /// <summary>
    /// Represents a game from the CollegeBasketballData API (api.collegebasketballdata.com).
    /// Mirrors the CfbdGameDto pattern used on the football side.
    /// </summary>
    public class BasketballGameDto
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("completed")]
        [JsonPropertyName("completed")]
        public bool Completed { get; set; }

        [JsonProperty("status")]
        [JsonPropertyName("status")]
        public string? StatusRaw { get; set; } // "scheduled", "in_progress", "completed"

        [JsonProperty("period")]
        [JsonPropertyName("period")]
        public int? Period { get; set; }

        [JsonProperty("clock")]
        [JsonPropertyName("clock")]
        public string? Clock { get; set; }

        // --- RAW TEAM DATA (scoreboard endpoint wraps teams in objects) ---
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

        // --- SMART WRAPPERS ---
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
                if (HomeIdRaw.HasValue) return HomeIdRaw.Value;
                return GetInt(HomeRaw, "id");
            }
        }

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public int? AwayId
        {
            get
            {
                if (AwayIdRaw.HasValue) return AwayIdRaw.Value;
                return GetInt(AwayRaw, "id");
            }
        }

        // --- BILINGUAL EXTRACTION LOGIC (handles Dictionary, JObject, JsonElement) ---
        private static string? GetValue(object? raw, string key)
        {
            if (raw == null) return null;
            if (raw is string s) return s;
            if (raw is IDictionary<string, object> dict)
                return dict.TryGetValue(key, out var val) ? val?.ToString() : null;
            if (raw is JObject jo) return jo[key]?.ToString();
            if (raw is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.String) return je.GetString();
                if (je.ValueKind == JsonValueKind.Object && je.TryGetProperty(key, out var prop))
                    return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
            }
            return null;
        }

        private static int? GetInt(object? raw, string key)
        {
            if (raw == null) return null;
            if (raw is IDictionary<string, object> dict)
            {
                if (dict.TryGetValue(key, out var val) && val != null)
                    try { return Convert.ToInt32(val); } catch { return null; }
                return null;
            }
            if (raw is JObject jo) return jo[key]?.Value<int?>();
            if (raw is JsonElement je && je.ValueKind == JsonValueKind.Object &&
                je.TryGetProperty(key, out var prop) && prop.TryGetInt32(out int v))
                return v;
            return null;
        }

        [JsonProperty("notes")]
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }
}
