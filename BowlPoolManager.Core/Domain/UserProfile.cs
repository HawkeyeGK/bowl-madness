using System.Text.Json.Serialization;

namespace BowlPoolManager.Core.Domain
{
    public class UserProfile
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty; // Will match the Auth Provider ID

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("roles")]
        public List<string> Roles { get; set; } = new List<string>();

        [JsonPropertyName("type")]
        public string Type { get; set; } = "UserProfile";
    }
}
