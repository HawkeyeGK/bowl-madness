using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace BowlPoolManager.Core.Domain
{
    public class UserProfile
    {
        [JsonProperty("id")] // <--- Critical for Cosmos SDK
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty; // This will be the SWA User ID

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        // Roles: "SuperAdmin", "Admin", "Player"
        [JsonProperty("appRole")]
        [JsonPropertyName("appRole")]
        public string AppRole { get; set; } = "Player";

        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public string Type { get; set; } = "UserProfile";
        
        [JsonPropertyName("createdOn")]
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    }
}
