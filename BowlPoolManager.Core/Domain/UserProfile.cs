using System.Text.Json.Serialization;

namespace BowlPoolManager.Core.Domain
{
    public class UserProfile
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty; // This will be the SWA User ID

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        // Roles: "SuperAdmin", "Admin", "Player"
        [JsonPropertyName("appRole")]
        public string AppRole { get; set; } = "Player";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "UserProfile";
        
        [JsonPropertyName("createdOn")]
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    }
}
