using System.Text.Json.Serialization;
using Newtonsoft.Json;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Dtos
{
    /// <summary>
    /// What the Game Results page may change on a football game: its status and the two scores. The server applies
    /// these to the stored game, so nothing else the page loaded (teams, slots, feed data) can be written back stale
    /// (issue 138).
    /// </summary>
    public class GameResultUpdate
    {
        [JsonProperty("gameId")]
        [JsonPropertyName("gameId")]
        public string GameId { get; set; } = string.Empty;

        [JsonProperty("seasonId")]
        [JsonPropertyName("seasonId")]
        public string SeasonId { get; set; } = string.Empty;

        [JsonProperty("gameStatus")]
        [JsonPropertyName("gameStatus")]
        public GameStatus Status { get; set; } = GameStatus.Scheduled;

        [JsonProperty("teamHomeScore")]
        [JsonPropertyName("teamHomeScore")]
        public int? TeamHomeScore { get; set; }

        [JsonProperty("teamAwayScore")]
        [JsonPropertyName("teamAwayScore")]
        public int? TeamAwayScore { get; set; }
    }
}
