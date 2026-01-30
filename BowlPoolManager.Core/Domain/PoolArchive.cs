using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace BowlPoolManager.Core.Domain
{
    public class PoolArchive
    {
        [JsonProperty("id")] // Format: "Archive_{PoolId}"
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("poolId")]
        [JsonPropertyName("poolId")]
        public string PoolId { get; set; } = string.Empty;

        [JsonProperty("poolName")]
        [JsonPropertyName("poolName")]
        public string PoolName { get; set; } = string.Empty;

        [JsonProperty("seasonId")]
        [JsonPropertyName("seasonId")]
        public string SeasonId { get; set; } = string.Empty;

        [JsonProperty("season")]
        [JsonPropertyName("season")]
        public int Season { get; set; }

        [JsonProperty("archivedOn")]
        [JsonPropertyName("archivedOn")]
        public DateTime ArchivedOn { get; set; } = DateTime.UtcNow;

        [JsonProperty("games")]
        [JsonPropertyName("games")]
        public List<ArchiveGame> Games { get; set; } = new();

        [JsonProperty("standings")]
        [JsonPropertyName("standings")]
        public List<ArchiveStanding> Standings { get; set; } = new();
        
        // Cosmos Discriminator
        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public string Type { get; set; } = "PoolArchive";
    }

    public class ArchiveGame
    {
        [JsonProperty("gameId")]
        [JsonPropertyName("gameId")]
        public string GameId { get; set; } = string.Empty;

        [JsonProperty("startTime")]
        [JsonPropertyName("startTime")]
        public DateTime StartTime { get; set; } = DateTime.UtcNow;


        [JsonProperty("bowlName")]
        [JsonPropertyName("bowlName")]
        public string BowlName { get; set; } = string.Empty;

        [JsonProperty("teamHome")]
        [JsonPropertyName("teamHome")]
        public string TeamHome { get; set; } = string.Empty;

        [JsonProperty("teamHomeScore")]
        [JsonPropertyName("teamHomeScore")]
        public int? TeamHomeScore { get; set; }

        [JsonProperty("teamAway")]
        [JsonPropertyName("teamAway")]
        public string TeamAway { get; set; } = string.Empty;

        [JsonProperty("teamAwayScore")]
        [JsonPropertyName("teamAwayScore")]
        public int? TeamAwayScore { get; set; }

        [JsonProperty("pointValue")]
        [JsonPropertyName("pointValue")]
        public int PointValue { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string WinningTeamName
        {
            get
            {
                if (!TeamHomeScore.HasValue || !TeamAwayScore.HasValue) return "TBD";
                if (TeamHomeScore > TeamAwayScore) return TeamHome;
                if (TeamAwayScore > TeamHomeScore) return TeamAway;
                return "Tie";
            }
        }
    }

    public class ArchiveStanding
    {
        [JsonProperty("playerName")]
        [JsonPropertyName("playerName")]
        public string PlayerName { get; set; } = string.Empty;

        [JsonProperty("rank")]
        [JsonPropertyName("rank")]
        public int Rank { get; set; }

        [JsonProperty("totalPoints")]
        [JsonPropertyName("totalPoints")]
        public int TotalPoints { get; set; }

        [JsonProperty("correctPicks")]
        [JsonPropertyName("correctPicks")]
        public int CorrectPicks { get; set; }

        [JsonProperty("tieBreakerPoints")]
        [JsonPropertyName("tieBreakerPoints")]
        public int TieBreakerPoints { get; set; }

        [JsonProperty("tieBreakerDelta")]
        [JsonPropertyName("tieBreakerDelta")]
        public int? TieBreakerDelta { get; set; }

        // Key: GameId, Value: CreatePickedTeam
        [JsonProperty("picks")]
        [JsonPropertyName("picks")]
        public Dictionary<string, string> Picks { get; set; } = new();
    }
}
