using Newtonsoft.Json;

namespace BowlPoolManager.Core.Dtos
{
    public class IntegrationStatusDto
    {
        [JsonProperty("isApiKeyConfigured")]
        [System.Text.Json.Serialization.JsonPropertyName("isApiKeyConfigured")]
        public bool IsApiKeyConfigured { get; set; }

        [JsonProperty("lastSyncUtc")]
        [System.Text.Json.Serialization.JsonPropertyName("lastSyncUtc")]
        public DateTime? LastSyncUtc { get; set; }

        [JsonProperty("teamCount")]
        [System.Text.Json.Serialization.JsonPropertyName("teamCount")]
        public int TeamCount { get; set; }

        [JsonProperty("message")]
        [System.Text.Json.Serialization.JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("debugDetails")]
        [System.Text.Json.Serialization.JsonPropertyName("debugDetails")]
        public string DebugDetails { get; set; } = string.Empty;

        // Basketball integration status
        [JsonProperty("isBasketballApiKeyConfigured")]
        [System.Text.Json.Serialization.JsonPropertyName("isBasketballApiKeyConfigured")]
        public bool IsBasketballApiKeyConfigured { get; set; }

        [JsonProperty("basketballLastSyncUtc")]
        [System.Text.Json.Serialization.JsonPropertyName("basketballLastSyncUtc")]
        public DateTime? BasketballLastSyncUtc { get; set; }

        [JsonProperty("basketballTeamCount")]
        [System.Text.Json.Serialization.JsonPropertyName("basketballTeamCount")]
        public int BasketballTeamCount { get; set; }

        [JsonProperty("basketballMessage")]
        [System.Text.Json.Serialization.JsonPropertyName("basketballMessage")]
        public string BasketballMessage { get; set; } = string.Empty;
    }
}
