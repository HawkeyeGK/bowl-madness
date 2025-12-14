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

        // NEW: Replaces AdminUserId. Supports delegated admins.
        [JsonProperty("poolAdminIds")]
        [JsonPropertyName("poolAdminIds")]
        public List<string> PoolAdminIds { get; set; } = new List<string>();

        // NEW: Password required to join the pool.
        [JsonProperty("accessKey")]
        [JsonPropertyName("accessKey")]
        public string AccessKey { get; set; } = string.Empty;

        [JsonProperty("year")]
        [JsonPropertyName("year")]
        public int Year { get; set; } = DateTime.UtcNow.Year;

        // Removed: IsPublic (YAGNI - You Aint Gonna Need It)

        // Required for Cosmos DB discriminator
        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public string Type { get; set; } = "BowlPool";
    }
}
