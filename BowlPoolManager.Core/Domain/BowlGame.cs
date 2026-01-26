using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace BowlPoolManager.Core.Domain
{
    public class BowlGame
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("seasonId")]
        [JsonPropertyName("seasonId")]
        public string SeasonId { get; set; } = string.Empty;

        [JsonProperty("bowlName")]
        [JsonPropertyName("bowlName")]
        public string BowlName { get; set; } = string.Empty;

        // --- EXTERNAL LINKAGE ---
        [JsonProperty("externalId")]
        [JsonPropertyName("externalId")]
        public string? ExternalId { get; set; }

        // BRIDGE FIELDS
        // These store the exact API Team Name that corresponds to the local team.
        // ApiHomeTeam -> The API name for the team stored in 'TeamHome'
        // ApiAwayTeam -> The API name for the team stored in 'TeamAway'
        [JsonProperty("apiHomeTeam")]
        [JsonPropertyName("apiHomeTeam")]
        public string? ApiHomeTeam { get; set; }

        [JsonProperty("apiAwayTeam")]
        [JsonPropertyName("apiAwayTeam")]
        public string? ApiAwayTeam { get; set; }
        // ------------------------

        [JsonProperty("startTime")]
        [JsonPropertyName("startTime")]
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        [JsonProperty("gameStatus")]
        [JsonPropertyName("gameStatus")]
        public GameStatus Status { get; set; } = GameStatus.Scheduled;

        // NEW FIELD: Stores "3rd • 10:30", "Final", etc.
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

        [JsonProperty("homeTeamInfo")]
        [JsonPropertyName("homeTeamInfo")]
        public TeamInfo? HomeTeamInfo { get; set; }

        [JsonProperty("teamAway")]
        [JsonPropertyName("teamAway")]
        public string TeamAway { get; set; } = string.Empty;

        [JsonProperty("teamAwayId")]
        [JsonPropertyName("teamAwayId")]
        public int? TeamAwayId { get; set; }

        [JsonProperty("awayTeamInfo")]
        [JsonPropertyName("awayTeamInfo")]
        public TeamInfo? AwayTeamInfo { get; set; }

        // SEEDS
        [JsonProperty("teamHomeSeed")]
        [JsonPropertyName("teamHomeSeed")]
        public int? TeamHomeSeed { get; set; }

        [JsonProperty("teamAwaySeed")]
        [JsonPropertyName("teamAwaySeed")]
        public int? TeamAwaySeed { get; set; }

        // SCORES
        [JsonProperty("teamHomeScore")]
        [JsonPropertyName("teamHomeScore")]
        public int? TeamHomeScore { get; set; }

        [JsonProperty("teamAwayScore")]
        [JsonPropertyName("teamAwayScore")]
        public int? TeamAwayScore { get; set; }

        // SCORING
        [JsonProperty("pointValue")]
        [JsonPropertyName("pointValue")]
        public int PointValue { get; set; } = 1;

        [JsonProperty("isPlayoff")]
        [JsonPropertyName("isPlayoff")]
        public bool IsPlayoff { get; set; } = false;

        [JsonProperty("round")]
        [JsonPropertyName("round")]
        public PlayoffRound Round { get; set; } = PlayoffRound.Standard;

        [JsonProperty("nextGameId")]
        [JsonPropertyName("nextGameId")]
        public string? NextGameId { get; set; }

        [JsonProperty("location")]
        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonProperty("television")]
        [JsonPropertyName("television")]
        public string? Television { get; set; }

        // --- COMPUTED HELPERS (Logic Consolidation) ---
        
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
                // Treat null score as 0 for comparison
                int home = TeamHomeScore ?? 0;
                int away = TeamAwayScore ?? 0;
                
                if (home > away) return TeamHome;
                if (away > home) return TeamAway;
                return null; // Tie or error
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

        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public string Type { get; set; } = "BowlGame";
    }
}
