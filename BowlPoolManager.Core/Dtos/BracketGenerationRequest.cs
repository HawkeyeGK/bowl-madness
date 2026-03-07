using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace BowlPoolManager.Core.Dtos
{
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
        /// E.g. [["East","West"],["South","Midwest"]] means East plays West in FF semifinal 1,
        /// South plays Midwest in FF semifinal 2.
        /// </summary>
        [JsonProperty("finalFourPairings")]
        [JsonPropertyName("finalFourPairings")]
        public List<List<string>> FinalFourPairings { get; set; } = new();
    }
}
