using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace BowlPoolManager.Core.Domain
{
    public class BowlPool
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("name")]
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("adminUserId")]
        [JsonPropertyName("adminUserId")]
        public string AdminUserId { get; set; } = string.Empty;

        [JsonProperty("year")]
        [JsonPropertyName("year")]
        public int Year { get; set; } = DateTime.UtcNow.Year;

        [JsonProperty("isPublic")]
        [JsonPropertyName("isPublic")]
        public bool IsPublic { get; set; } = false;

        // Partition Key for Cosmos DB (usually /type or /id)
        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public string Type { get; set; } = "BowlPool";
    }
}
