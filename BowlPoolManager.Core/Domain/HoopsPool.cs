using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace BowlPoolManager.Core.Domain
{
    public class HoopsPool
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("seasonId")]
        [JsonPropertyName("seasonId")]
        public string SeasonId { get; set; } = string.Empty;

        [JsonProperty("name")]
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("season")]
        [JsonPropertyName("season")]
        public int Season { get; set; } = DateTime.UtcNow.Year;

        [JsonProperty("gameIds")]
        [JsonPropertyName("gameIds")]
        public List<string> GameIds { get; set; } = new List<string>();

        [JsonProperty("lockDate")]
        [JsonPropertyName("lockDate")]
        public DateTime LockDate { get; set; } = DateTime.UtcNow.AddDays(7);

        [JsonProperty("inviteCode")]
        [JsonPropertyName("inviteCode")]
        public string InviteCode { get; set; } = string.Empty;

        [JsonProperty("isConcluded")]
        [JsonPropertyName("isConcluded")]
        public bool IsConcluded { get; set; } = false;

        [JsonProperty("isArchived")]
        [JsonPropertyName("isArchived")]
        public bool IsArchived { get; set; } = false;

        // Round-based scoring: maps each TournamentRound to a point value
        [JsonProperty("pointsPerRound")]
        [JsonPropertyName("pointsPerRound")]
        public Dictionary<TournamentRound, int>? PointsPerRound { get; set; }

        // COSMOS DISCRIMINATOR
        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public string Type { get; set; } = Constants.DocumentTypes.HoopsPool;
    }
}
