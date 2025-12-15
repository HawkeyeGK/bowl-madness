using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace BowlPoolManager.Core.Domain
{
    public class BracketEntry
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // The name of the person (Entered by Admin)
        [JsonProperty("playerName")]
        [JsonPropertyName("playerName")]
        public string PlayerName { get; set; } = string.Empty;

        // Dictionary: Key = GameId, Value = Selected Team Name
        [JsonProperty("picks")]
        [JsonPropertyName("picks")]
        public Dictionary<string, string> Picks { get; set; } = new Dictionary<string, string>();

        // Predicted total points for the Championship Game (Standard Tiebreaker)
        [JsonProperty("tieBreakerPoints")]
        [JsonPropertyName("tieBreakerPoints")]
        public int TieBreakerPoints { get; set; } = 0;

        // Metadata
        [JsonProperty("createdOn")]
        [JsonPropertyName("createdOn")]
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        // COSMOS DISCRIMINATOR
        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public string Type { get; set; } = Constants.DocumentTypes.BracketEntry;
    }
}
