using Microsoft.Extensions.Configuration;

namespace BowlPoolManager.Api.Helpers
{
    public interface ITestEnvironment
    {
        bool IsEnabled { get; }
    }

    // Both conditions must hold: SiteEnvironment is exactly "Test", and the Cosmos account is the
    // test account. The account check is an allow-list, so an unknown or look-alike host is refused
    // as well as the production account. Production never sets SiteEnvironment.
    public class TestEnvironment : ITestEnvironment
    {
        public const string SiteEnvironmentSetting = "SiteEnvironment";
        public const string TestSiteEnvironment = "Test";
        public const string TestAccountName = "cosmos-bowlmadness-test";
        public const string ProductionAccountName = "cosmos-bowlmadness-prod";
        public const string CosmosHostSuffix = "documents.azure.com";

        public bool IsEnabled { get; }

        public TestEnvironment(IConfiguration configuration, Uri? cosmosEndpoint)
            : this(configuration[SiteEnvironmentSetting], cosmosEndpoint) { }

        public TestEnvironment(string? siteEnvironment, Uri? cosmosEndpoint)
        {
            IsEnabled = Evaluate(siteEnvironment, cosmosEndpoint);
        }

        public static bool Evaluate(string? siteEnvironment, Uri? cosmosEndpoint)
        {
            if (!string.Equals(siteEnvironment, TestSiteEnvironment, StringComparison.Ordinal)) return false;
            if (cosmosEndpoint == null) return false;

            var host = cosmosEndpoint.Host;
            if (host.StartsWith(ProductionAccountName + ".", StringComparison.OrdinalIgnoreCase)) return false;

            return string.Equals(host, $"{TestAccountName}.{CosmosHostSuffix}", StringComparison.OrdinalIgnoreCase);
        }
    }
}