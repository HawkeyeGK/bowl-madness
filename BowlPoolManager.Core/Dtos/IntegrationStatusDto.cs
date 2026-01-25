namespace BowlPoolManager.Core.Dtos
{
    public class IntegrationStatusDto
    {
        public bool IsApiKeyConfigured { get; set; }
        public DateTime? LastSyncUtc { get; set; }
        public int TeamCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
