using FluentAssertions;
using Microsoft.Extensions.Configuration;
using BowlPoolManager.Api.Helpers;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// TestEnvironment is the only thing that keeps ResetTestData away from production, so these
    /// tests cover both conditions (SiteEnvironment and the Cosmos account) and the ways each can be wrong.
    /// Endpoints are built from the account-name constants so no host name is written out here.
    /// </summary>
    public class TestEnvironmentTests
    {
        private static Uri Endpoint(string account, string suffix = TestEnvironment.CosmosHostSuffix) =>
            new($"https://{account}.{suffix}:443/");

        private static readonly Uri TestAccount = Endpoint(TestEnvironment.TestAccountName);
        private static readonly Uri ProductionAccount = Endpoint(TestEnvironment.ProductionAccountName);

        [Fact]
        public void Evaluate_ShouldBeTrue_WhenSiteEnvironmentIsTestAndAccountIsTestAccount()
        {
            TestEnvironment.Evaluate("Test", TestAccount).Should().BeTrue();
        }

        [Fact]
        public void Evaluate_ShouldBeFalse_WhenAccountIsProduction_EvenIfSiteEnvironmentIsTest()
        {
            TestEnvironment.Evaluate("Test", ProductionAccount).Should().BeFalse();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Production")]
        [InlineData("test")]
        [InlineData("TEST")]
        [InlineData(" Test")]
        [InlineData("Test ")]
        [InlineData("Testing")]
        public void Evaluate_ShouldBeFalse_WhenSiteEnvironmentIsMissingOrNotExactlyTest(string? siteEnvironment)
        {
            TestEnvironment.Evaluate(siteEnvironment, TestAccount).Should().BeFalse();
        }

        [Fact]
        public void Evaluate_ShouldBeFalse_WhenEndpointIsMissing()
        {
            TestEnvironment.Evaluate("Test", null).Should().BeFalse();
        }

        [Fact]
        public void Evaluate_ShouldBeFalse_WhenAccountIsAnotherAccount()
        {
            TestEnvironment.Evaluate("Test", Endpoint("some-other-account")).Should().BeFalse();
        }

        [Fact]
        public void Evaluate_ShouldBeFalse_WhenAccountIsTheEmulator()
        {
            TestEnvironment.Evaluate("Test", new Uri("https://localhost:8081/")).Should().BeFalse();
        }

        [Fact]
        public void Evaluate_ShouldBeFalse_WhenTestAccountNameIsOnAnotherDomain()
        {
            TestEnvironment.Evaluate("Test", Endpoint(TestEnvironment.TestAccountName, "example.com")).Should().BeFalse();
        }

        [Fact]
        public void Evaluate_ShouldBeFalse_WhenHostOnlyStartsWithTheTestHost()
        {
            var lookalike = Endpoint(TestEnvironment.TestAccountName, TestEnvironment.CosmosHostSuffix + ".evil.example");
            TestEnvironment.Evaluate("Test", lookalike).Should().BeFalse();
        }

        [Fact]
        public void Evaluate_ShouldBeFalse_WhenAccountNameOnlyEndsWithTheTestName()
        {
            TestEnvironment.Evaluate("Test", Endpoint("evil-" + TestEnvironment.TestAccountName)).Should().BeFalse();
        }

        [Fact]
        public void IsEnabled_ShouldBeFalse_WhenBuiltFromProductionEndpoint()
        {
            new TestEnvironment("Test", ProductionAccount).IsEnabled.Should().BeFalse();
        }

        [Fact]
        public void IsEnabled_ShouldBeTrue_WhenBuiltFromTestEndpointAndTestSetting()
        {
            new TestEnvironment("Test", TestAccount).IsEnabled.Should().BeTrue();
        }

        [Fact]
        public void IsEnabled_ShouldBeFalse_WhenBuiltFromEmptyConfiguration()
        {
            var config = new ConfigurationBuilder().Build();
            new TestEnvironment(config, TestAccount).IsEnabled.Should().BeFalse();
        }

        [Fact]
        public void IsEnabled_ShouldReadSiteEnvironmentSetting_WhenBuiltFromConfiguration()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["SiteEnvironment"] = "Test" })
                .Build();

            new TestEnvironment(config, TestAccount).IsEnabled.Should().BeTrue();
            new TestEnvironment(config, ProductionAccount).IsEnabled.Should().BeFalse();
        }
    }
}
