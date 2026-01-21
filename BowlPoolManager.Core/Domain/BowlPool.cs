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

        // NEW: The Master Deadline (No picks/entries after this)
        [JsonProperty("lockDate")]
        [JsonPropertyName("lockDate")]
        public DateTime LockDate { get; set; } = DateTime.UtcNow.AddDays(7); 

        // NEW: The Gatekeeper Password
        [JsonProperty("inviteCode")]
        [JsonPropertyName("inviteCode")]
        public string InviteCode { get; set; } = string.Empty;

        // COSMOS DISCRIMINATOR
        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public string Type { get; set; } = Constants.DocumentTypes.BowlPool;
    }
}
