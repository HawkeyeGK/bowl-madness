using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;

namespace BowlPoolManager.Api.Helpers
{
    public static class ApplicationInsightsLogging
    {
        // The Application Insights SDK adds a filter that sends only Warning and above to
        // Application Insights. This lets Information through for our own code (every logger in
        // the API is ILogger<T> or ILoggerFactory.CreateLogger<T>() on a class under
        // BowlPoolManager.Api). Framework categories such as Microsoft.* and System.* stay at Warning.
        public static ILoggingBuilder AllowAppInformationLogs(this ILoggingBuilder logging)
        {
            return logging.AddFilter<ApplicationInsightsLoggerProvider>("BowlPoolManager.Api.", LogLevel.Information);
        }
    }
}
