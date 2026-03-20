using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace BowlPoolManager.Core.Domain
{
    public class HoopsGame : IScorable
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("seasonId")]
        [JsonPropertyName("seasonId")]
        public string SeasonId { get; set; } = string.Empty;

        [JsonProperty("round")]
        [JsonPropertyName("round")]
        public TournamentRound Round { get; set; } = TournamentRound.RoundOf64;

        [JsonProperty("region")]
        [JsonPropertyName("region")]
        public string? Region { get; set; }

        // --- EXTERNAL LINKAGE ---
        [JsonProperty("externalId")]
        [JsonPropertyName("externalId")]
        public string? ExternalId { get; set; }

        // BRIDGE FIELDS (for team name mismatches with the external API)
        [JsonProperty("apiHomeTeam")]
        [JsonPropertyName("apiHomeTeam")]
        public string? ApiHomeTeam { get; set; }

        [JsonProperty("apiAwayTeam")]
        [JsonPropertyName("apiAwayTeam")]
        public string? ApiAwayTeam { get; set; }

        [JsonProperty("startTime")]
        [JsonPropertyName("startTime")]
        public DateTime? StartTime { get; set; }

        [JsonProperty("television")]
        [JsonPropertyName("television")]
        public string? Television { get; set; }

        [JsonProperty("gameStatus")]
        [JsonPropertyName("gameStatus")]
        public GameStatus Status { get; set; } = GameStatus.Scheduled;

        [JsonProperty("gameDetail")]
        [JsonPropertyName("gameDetail")]
        public string? GameDetail { get; set; }

        // TEAMS
        [JsonProperty("teamHome")]
        [JsonPropertyName("teamHome")]
        public string TeamHome { get; set; } = string.Empty;

        [JsonProperty("teamHomeId")]
        [JsonPropertyName("teamHomeId")]
        public int? TeamHomeId { get; set; }

        [JsonProperty("teamHomeSeed")]
        [JsonPropertyName("teamHomeSeed")]
        public int? TeamHomeSeed { get; set; }

        [JsonProperty("homeTeamInfo")]
        [JsonPropertyName("homeTeamInfo")]
        public TeamInfo? HomeTeamInfo { get; set; }

        [JsonProperty("teamAway")]
        [JsonPropertyName("teamAway")]
        public string TeamAway { get; set; } = string.Empty;

        [JsonProperty("teamAwayId")]
        [JsonPropertyName("teamAwayId")]
        public int? TeamAwayId { get; set; }

        [JsonProperty("teamAwaySeed")]
        [JsonPropertyName("teamAwaySeed")]
        public int? TeamAwaySeed { get; set; }

        [JsonProperty("awayTeamInfo")]
        [JsonPropertyName("awayTeamInfo")]
        public TeamInfo? AwayTeamInfo { get; set; }

        // SCORES
        [JsonProperty("teamHomeScore")]
        [JsonPropertyName("teamHomeScore")]
        public int? TeamHomeScore { get; set; }

        [JsonProperty("teamAwayScore")]
        [JsonPropertyName("teamAwayScore")]
        public int? TeamAwayScore { get; set; }

        // SCORING — defaults to 0 so missed hydration is immediately visible
        [JsonProperty("pointValue")]
        [JsonPropertyName("pointValue")]
        public int PointValue { get; set; } = 0;

        // BRACKET WIRING
        [JsonProperty("nextGameId")]
        [JsonPropertyName("nextGameId")]
        public string? NextGameId { get; set; }

        /// <summary>
        /// Seed matchup label for first-round games, set by the bracket generator.
        /// e.g. "1v16", "8v9", "11v11", "16v16". Null for later-round games.
        /// </summary>
        [JsonProperty("seedMatchup")]
        [JsonPropertyName("seedMatchup")]
        public string? SeedMatchup { get; set; }

        // --- COMPUTED HELPERS ---

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsFinal => Status == GameStatus.Final;

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string? WinningTeamName
        {
            get
            {
                if (Status != GameStatus.Final) return null;
                int home = TeamHomeScore ?? 0;
                int away = TeamAwayScore ?? 0;
                if (home > away) return TeamHome;
                if (away > home) return TeamAway;
                return null;
            }
        }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string? LosingTeamName
        {
            get
            {
                if (Status != GameStatus.Final) return null;
                int home = TeamHomeScore ?? 0;
                int away = TeamAwayScore ?? 0;
                if (home < away) return TeamHome;
                if (away < home) return TeamAway;
                return null;
            }
        }

        // COSMOS DISCRIMINATOR
        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public string Type { get; set; } = Constants.DocumentTypes.HoopsGame;
    }
}
