using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace BowlPoolManager.Core.Domain
{
    public class BowlPool
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("seasonId")]
        [JsonPropertyName("seasonId")]
        public string SeasonId { get; set; } = Constants.CurrentSeason;

        [JsonProperty("name")]
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("season")]
        [JsonPropertyName("season")]
        public int Season { get; set; } = DateTime.Now.Year;

        // NEW: Explicitly list games in this pool (Phase 4 Decoupling)
        [JsonProperty("gameIds")]
        [JsonPropertyName("gameIds")]
        public List<string> GameIds { get; set; } = new List<string>();

        // NEW: The Master Deadline (No picks/entries after this)
        [JsonProperty("lockDate")]
        [JsonPropertyName("lockDate")]
        public DateTime LockDate { get; set; } = DateTime.UtcNow.AddDays(7); 

        // NEW: The Gatekeeper Password
        [JsonProperty("inviteCode")]
        [JsonPropertyName("inviteCode")]
        public string InviteCode { get; set; } = string.Empty;

        // NEW: Phase 3.1 - Soft Archive support
        [JsonProperty("isConcluded")]
        [JsonPropertyName("isConcluded")]
        public bool IsConcluded { get; set; } = false;

        [JsonProperty("isArchived")]
        [JsonPropertyName("isArchived")]
        public bool IsArchived { get; set; } = false;

        // COSMOS DISCRIMINATOR
        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public string Type { get; set; } = Constants.DocumentTypes.BowlPool;
    }
}
