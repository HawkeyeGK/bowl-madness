using System.Text.Json.Serialization;
using Newtonsoft.Json;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Dtos
{
    public class LeaderboardDto
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("playerName")]
        [JsonPropertyName("playerName")]
        public string PlayerName { get; set; } = string.Empty;

        [JsonProperty("rank")]
        [JsonPropertyName("rank")]
        public int Rank { get; set; }

        [JsonProperty("score")]
        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonProperty("maxPossible")]
        [JsonPropertyName("maxPossible")]
        public int MaxPossible { get; set; }

        [JsonProperty("correctPicks")]
        [JsonPropertyName("correctPicks")]
        public int CorrectPicks { get; set; }

        [JsonProperty("tieBreakerPoints")]
        [JsonPropertyName("tieBreakerPoints")]
        public int TieBreakerPoints { get; set; }

        [JsonProperty("tieBreakerDelta")]
        [JsonPropertyName("tieBreakerDelta")]
        public int? TieBreakerDelta { get; set; }

        [JsonProperty("isPaid")]
        [JsonPropertyName("isPaid")]
        public bool IsPaid { get; set; }

        [JsonProperty("roundScores")]
        [JsonPropertyName("roundScores")]
        public Dictionary<PlayoffRound, int> RoundScores { get; set; } = new();
    }
}
