using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace BowlPoolManager.Core.Dtos
{
    /// <summary>
    /// Describes one of the four First Four play-in games: which region it feeds into
    /// and which seed the winner assumes in that region's Round of 64.
    /// </summary>
    public class FirstFourEntry
    {
        [JsonProperty("region")]
        [JsonPropertyName("region")]
        public string Region { get; set; } = string.Empty;

        [JsonProperty("seed")]
        [JsonPropertyName("seed")]
        public int Seed { get; set; }
    }

    public class BracketGenerationRequest
    {
        [JsonProperty("poolId")]
        [JsonPropertyName("poolId")]
        public string PoolId { get; set; } = string.Empty;

        [JsonProperty("seasonId")]
        [JsonPropertyName("seasonId")]
        public string SeasonId { get; set; } = string.Empty;

        /// <summary>4 region names in bracket order (e.g. ["East","West","South","Midwest"]).</summary>
        [JsonProperty("regions")]
        [JsonPropertyName("regions")]
        public List<string> Regions { get; set; } = new();

        /// <summary>
        /// Two pairs of region names that meet in the Final Four.
        /// E.g. [["South","West"],["East","Midwest"]] means South plays West in FF semifinal 1,
        /// East plays Midwest in FF semifinal 2.
        /// </summary>
        [JsonProperty("finalFourPairings")]
        [JsonPropertyName("finalFourPairings")]
        public List<List<string>> FinalFourPairings { get; set; } = new();

        /// <summary>
        /// Exactly 4 entries, one per First Four play-in game. Each entry specifies the region
        /// and seed that the winner advances to (e.g. Region="South", Seed=16 means the winner
        /// becomes the 16-seed in the South's Round of 64).
        /// </summary>
        [JsonProperty("firstFourGames")]
        [JsonPropertyName("firstFourGames")]
        public List<FirstFourEntry> FirstFourGames { get; set; } = new();
    }
}
