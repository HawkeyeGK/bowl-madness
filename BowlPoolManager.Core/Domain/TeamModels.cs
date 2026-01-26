using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace BowlPoolManager.Core.Domain
{
    public class TeamConfig
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = "Config_Teams_FBS";

        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public string Type { get; set; } = Constants.DocumentTypes.TeamConfig;

        [JsonProperty("teams")]
        [JsonPropertyName("teams")]
        public List<TeamInfo> Teams { get; set; } = new();

        [JsonProperty("lastUpdated")]
        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class TeamInfo
    {
        [JsonProperty("schoolId")]
        [JsonPropertyName("schoolId")]
        public int SchoolId { get; set; }

        [JsonProperty("school")]
        [JsonPropertyName("school")]
        public string School { get; set; } = string.Empty;

        [JsonProperty("mascot")]
        [JsonPropertyName("mascot")]
        public string Mascot { get; set; } = string.Empty;

        [JsonProperty("abbreviation")]
        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;

        [JsonProperty("conference")]
        [JsonPropertyName("conference")]
        public string Conference { get; set; } = string.Empty;

        [JsonProperty("color")]
        [JsonPropertyName("color")]
        public string Color { get; set; } = string.Empty;

        [JsonProperty("alternateColor")]
        [JsonPropertyName("alternateColor")]
        public string AltColor { get; set; } = string.Empty;

        [JsonProperty("logos", NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("logos")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Logos { get; set; } = new();

        // Helper Property (Computed + Backing Field)
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public string DisplayName => $"{School} {Mascot}";

        private string? _primaryLogoUrl;
        [JsonProperty("primaryLogoUrl")]
        [JsonPropertyName("primaryLogoUrl")]
        public string PrimaryLogoUrl 
        {
            get
            {
                // If explicitly set (e.g. from denormalized object), return it
                if (!string.IsNullOrEmpty(_primaryLogoUrl)) return _primaryLogoUrl;

                if (Logos == null || !Logos.Any()) return string.Empty;
                // Return the first logo that doesn't have "dark" in the path, or fallback to the first one available.
                return Logos.FirstOrDefault(l => !l.Contains("dark", StringComparison.OrdinalIgnoreCase)) ?? Logos.First();
            }
            set => _primaryLogoUrl = value;
        }

        private string? _darkLogoUrl;
        [JsonProperty("darkLogoUrl")]
        [JsonPropertyName("darkLogoUrl")]
        public string? DarkLogoUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(_darkLogoUrl)) return _darkLogoUrl;

                if (Logos == null || !Logos.Any()) return null;
                // Return the first logo that DOES have "dark" in the path.
                return Logos.FirstOrDefault(l => l.Contains("dark", StringComparison.OrdinalIgnoreCase));
            }
            set => _darkLogoUrl = value;
        }

        public TeamInfo ToDenormalized()
        {
            return new TeamInfo
            {
                SchoolId = this.SchoolId,
                School = this.School,
                Mascot = this.Mascot,
                Abbreviation = this.Abbreviation,
                Conference = this.Conference,
                Color = this.Color,
                AltColor = this.AltColor,
                // Capture the computed values into the backing fields
                PrimaryLogoUrl = this.PrimaryLogoUrl,
                DarkLogoUrl = this.DarkLogoUrl,
                // Strip the source array
                Logos = null
            };
        }
    }
}
