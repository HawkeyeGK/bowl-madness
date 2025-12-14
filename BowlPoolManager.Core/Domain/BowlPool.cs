using System.Text.Json.Serialization;

namespace BowlPoolManager.Core.Domain
{
    public class BowlPool
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("adminUserId")]
        public string AdminUserId { get; set; } = string.Empty;

        [JsonPropertyName("year")]
        public int Year { get; set; } = DateTime.UtcNow.Year;

        [JsonPropertyName("isPublic")]
        public bool IsPublic { get; set; } = false;

        // Partition Key for Cosmos DB (usually /type or /id)
        [JsonPropertyName("type")]
        public string Type { get; set; } = "BowlPool";
    }
}
