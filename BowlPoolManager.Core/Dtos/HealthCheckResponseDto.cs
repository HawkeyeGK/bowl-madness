namespace BowlPoolManager.Core.Dtos
{
    public class HealthCheckResponseDto
    {
        public string Status { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Message { get; set; } = string.Empty;
    }
}
