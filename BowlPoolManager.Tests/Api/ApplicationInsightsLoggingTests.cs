using FluentAssertions;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Api.Helpers;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// The Application Insights SDK adds a filter that sends only Warning and above to Application
    /// Insights. These tests build the SDK registration the way Program.cs does, with a fake channel,
    /// and check which ILogger calls become trace telemetry. Every logger in the API comes from
    /// ILogger&lt;T&gt; or ILoggerFactory.CreateLogger&lt;T&gt;(), so categories are class names under
    /// BowlPoolManager.Api (Functions, Functions.Admin, Services). Nothing uses FunctionContext.GetLogger,
    /// so the Function.*.User category is not used and is not covered.
    /// </summary>
    public class ApplicationInsightsLoggingTests
    {
        private sealed class CapturingChannel : ITelemetryChannel
        {
            private readonly List<ITelemetry> _items = new();

            public IReadOnlyList<ITelemetry> Items
            {
                get { lock (_items) { return _items.ToList(); } }
            }

            public bool? DeveloperMode { get; set; }
            public string? EndpointAddress { get; set; }
            public void Send(ITelemetry item) { lock (_items) { _items.Add(item); } }
            public void Flush() { }
            public void Dispose() { }
        }

        private static (ILogger logger, CapturingChannel channel, ServiceProvider provider) Build(
            bool allowInformation, string category)
        {
            var channel = new CapturingChannel();
            var services = new ServiceCollection();

            // The host always supplies IConfiguration; ConfigureFunctionsApplicationInsights needs it.
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddLogging(logging =>
            {
                if (allowInformation) logging.AllowAppInformationLogs();
            });
            services
                .AddApplicationInsightsTelemetryWorkerService(options =>
                {
                    options.ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000";
                    options.EnableAdaptiveSampling = false;
                    options.EnableQuickPulseMetricStream = false;
                    options.EnableHeartbeat = false;
                    options.EnableDependencyTrackingTelemetryModule = false;
                    options.EnablePerformanceCounterCollectionModule = false;
                    options.EnableEventCounterCollectionModule = false;
                    options.AddAutoCollectedMetricExtractor = false;
                })
                .ConfigureFunctionsApplicationInsights();
            services.Configure<TelemetryConfiguration>(config => config.TelemetryChannel = channel);

            var provider = services.BuildServiceProvider();
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(category);
            return (logger, channel, provider);
        }

        private static IEnumerable<string> TraceMessages(CapturingChannel channel) =>
            channel.Items.OfType<TraceTelemetry>().Select(t => t.Message);

        [Theory]
        [InlineData("BowlPoolManager.Api.Functions.UserFunctions")]
        [InlineData("BowlPoolManager.Api.Functions.Admin.SyncTeams")]
        [InlineData("BowlPoolManager.Api.Services.CfbdService")]
        public async Task AllowAppInformationLogs_ShouldSendInformationTraces_WhenCategoryIsAppCode(string category)
        {
            var (logger, channel, provider) = Build(allowInformation: true, category);
            await using (provider)
            {
                logger.LogInformation("information-marker");

                TraceMessages(channel).Should().Contain("information-marker");
            }
        }

        [Theory]
        [InlineData("Microsoft.AspNetCore.Hosting.Diagnostics")]
        [InlineData("System.Net.Http.HttpClient.ICfbdService.ClientHandler")]
        [InlineData("Microsoft.Azure.Functions.Worker.FunctionsApplication")]
        public async Task AllowAppInformationLogs_ShouldDropInformationTraces_WhenCategoryIsFramework(string category)
        {
            var (logger, channel, provider) = Build(allowInformation: true, category);
            await using (provider)
            {
                logger.LogInformation("information-marker");

                TraceMessages(channel).Should().NotContain("information-marker");
            }
        }

        [Theory]
        [InlineData("BowlPoolManager.Api.Functions.UserFunctions")]
        [InlineData("Microsoft.AspNetCore.Hosting.Diagnostics")]
        public async Task AllowAppInformationLogs_ShouldStillSendWarnings_WhenCategoryIsAnything(string category)
        {
            var (logger, channel, provider) = Build(allowInformation: true, category);
            await using (provider)
            {
                logger.LogWarning("warning-marker");

                TraceMessages(channel).Should().Contain("warning-marker");
            }
        }

        [Fact]
        public async Task AllowAppInformationLogs_ShouldDropInformationTraces_WhenCategoryOnlyLooksLikeAppCode()
        {
            var (logger, channel, provider) = Build(allowInformation: true, "BowlPoolManager.ApiX.Sample");
            await using (provider)
            {
                logger.LogInformation("information-marker");

                TraceMessages(channel).Should().NotContain("information-marker");
            }
        }

        [Fact]
        public async Task DefaultSdkFilter_ShouldDropInformationTraces_WhenAllowAppInformationLogsIsNotCalled()
        {
            var (logger, channel, provider) = Build(
                allowInformation: false, "BowlPoolManager.Api.Functions.UserFunctions");
            await using (provider)
            {
                logger.LogInformation("information-marker");

                TraceMessages(channel).Should().NotContain("information-marker");
            }
        }
    }
}
