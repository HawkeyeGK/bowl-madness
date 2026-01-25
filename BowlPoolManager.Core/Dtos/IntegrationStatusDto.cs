namespace BowlPoolManager.Core.Dtos
{
    public class IntegrationStatusDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("isApiKeyConfigured")]
        public bool IsApiKeyConfigured { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("lastSyncUtc")]
        public DateTime? LastSyncUtc { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("teamCount")]
        public int TeamCount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
}
