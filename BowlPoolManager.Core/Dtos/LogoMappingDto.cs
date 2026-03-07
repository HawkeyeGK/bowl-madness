using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace BowlPoolManager.Core.Dtos
{
    public class LogoMappingDto
    {
        [JsonProperty("schoolId")]
        [JsonPropertyName("schoolId")]
        public int SchoolId { get; set; }

        [JsonProperty("logoUrl")]
        [JsonPropertyName("logoUrl")]
        public string LogoUrl { get; set; } = string.Empty;
    }
}
