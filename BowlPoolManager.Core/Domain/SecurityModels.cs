using System.Text.Json.Serialization;

namespace BowlPoolManager.Core.Domain
{
    public class ClientPrincipal
    {
        [JsonPropertyName("identityProvider")]
        public string IdentityProvider { get; set; } = string.Empty;

        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("userDetails")]
        public string UserDetails { get; set; } = string.Empty;

        [JsonPropertyName("userRoles")]
        public IEnumerable<string> UserRoles { get; set; } = new List<string>();
    }

    public class AuthenticationData
    {
        [JsonPropertyName("clientPrincipal")]
        public ClientPrincipal? ClientPrincipal { get; set; }
    }
}
