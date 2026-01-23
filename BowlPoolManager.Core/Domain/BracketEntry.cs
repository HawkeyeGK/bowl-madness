using System.Text.Json.Serialization;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace BowlPoolManager.Core.Domain
{
    public class BracketEntry
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("seasonId")]
        [JsonPropertyName("seasonId")]
        public string SeasonId { get; set; } = string.Empty;

        [JsonProperty("poolId")]
        [JsonPropertyName("poolId")]
        public string PoolId { get; set; } = string.Empty;

        [JsonProperty("userId")]
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bracket Name is required.")]
        [JsonProperty("playerName")]
        [JsonPropertyName("playerName")]
        public string PlayerName { get; set; } = string.Empty;

        // FIXED: Made Nullable (?) to support "Redaction" (API setting it to null)
        [JsonProperty("picks")]
        [JsonPropertyName("picks")]
        public Dictionary<string, string>? Picks { get; set; } = new Dictionary<string, string>();

        [JsonProperty("tieBreakerPoints")]
        [JsonPropertyName("tieBreakerPoints")]
        public int TieBreakerPoints { get; set; } = 0;

        [JsonProperty("createdOn")]
        [JsonPropertyName("createdOn")]
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public string Type { get; set; } = Constants.DocumentTypes.BracketEntry;
    }
}
