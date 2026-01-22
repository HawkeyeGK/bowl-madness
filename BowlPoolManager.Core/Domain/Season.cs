using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace BowlPoolManager.Core.Domain
{
    public class Season
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty; // e.g. "2025"

        // Required for the Container's Partition Key
        [JsonProperty("seasonId")]
        [JsonPropertyName("seasonId")]
        public string SeasonId
        {
            get => Id;
            set { /* allow setting, but it fundamentally mirrors Id */ }
        }

        [JsonProperty("name")]
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty; // e.g. "2025-2026 Season"

        [JsonProperty("isCurrent")]
        [JsonPropertyName("isCurrent")]
        public bool IsCurrent { get; set; } = false;

        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public string Type { get; set; } = "Season";
    }
}
