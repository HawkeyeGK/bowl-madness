using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace BowlPoolManager.Core.Dtos
{
    /// <summary>
    /// Represents a game from the ESPN NCAAM basketball scoreboard API.
    /// Used by the HoopsGameLinker to match local HoopsGame shells to external API games.
    /// </summary>
    public class EspnScoreboardGameDto
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("homeTeam")]
        [JsonPropertyName("homeTeam")]
        public string? HomeTeam { get; set; }

        [JsonProperty("awayTeam")]
        [JsonPropertyName("awayTeam")]
        public string? AwayTeam { get; set; }

        [JsonProperty("homeId")]
        [JsonPropertyName("homeId")]
        public int? HomeId { get; set; }

        [JsonProperty("awayId")]
        [JsonPropertyName("awayId")]
        public int? AwayId { get; set; }

        [JsonProperty("homePoints")]
        [JsonPropertyName("homePoints")]
        public int? HomePoints { get; set; }

        [JsonProperty("awayPoints")]
        [JsonPropertyName("awayPoints")]
        public int? AwayPoints { get; set; }

        [JsonProperty("completed")]
        [JsonPropertyName("completed")]
        public bool Completed { get; set; }

        /// <summary>
        /// ESPN status type name, e.g. "STATUS_SCHEDULED", "STATUS_IN_PROGRESS",
        /// "STATUS_HALFTIME", "STATUS_FINAL".
        /// </summary>
        [JsonProperty("statusName")]
        [JsonPropertyName("statusName")]
        public string? StatusName { get; set; }

        /// <summary>Half number (1 or 2) or overtime period.</summary>
        [JsonProperty("period")]
        [JsonPropertyName("period")]
        public int? Period { get; set; }

        /// <summary>Formatted game clock, e.g. "12:34".</summary>
        [JsonProperty("displayClock")]
        [JsonPropertyName("displayClock")]
        public string? DisplayClock { get; set; }
    }
}
