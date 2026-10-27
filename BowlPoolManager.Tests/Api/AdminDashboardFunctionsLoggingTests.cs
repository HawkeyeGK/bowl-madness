using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Moq;
using BowlPoolManager.Api.Functions.Admin;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// Checks that GetIntegrationStatus does not write environment variable names or the API key
    /// length to the log. The status is still returned to the signed-in super admin in the response.
    /// </summary>
    public class AdminDashboardFunctionsLoggingTests
    {
        [Fact]
        public async Task GetIntegrationStatus_ShouldNotLogEnvironmentDetails_WhenCalledBySuperAdmin()
        {
            var ctx = new Mock<FunctionContext>().Object;

            var headers = new HttpHeadersCollection();
            headers.Add("x-ms-client-principal", Convert.ToBase64String(
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    userId = "user-admin",
                    userDetails = "admin@example.com",
                    identityProvider = "aad",
                    userRoles = new[] { "authenticated" }
                }))));

            var requestMock = new Mock<HttpRequestData>(ctx);
            requestMock.Setup(r => r.Headers).Returns(headers);
            var responseMock = new Mock<HttpResponseData>(ctx);
            responseMock.SetupProperty(r => r.StatusCode);
            responseMock.Setup(r => r.Body).Returns(new MemoryStream());
            requestMock.Setup(r => r.CreateResponse()).Returns(responseMock.Object);

            var userRepo = new Mock<IUserRepository>();
            userRepo
                .Setup(r => r.GetUserAsync(It.IsAny<string>()))
                .ReturnsAsync(new UserProfile { AppRole = BowlPoolManager.Core.Constants.Roles.SuperAdmin });

            var cosmos = new Mock<CosmosClient>();
            cosmos
                .Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new Mock<Container>().Object);

            var loggerFactory = new CapturingLoggerFactory();
            var sut = new AdminDashboardFunctions(
                cosmos.Object,
                new Logger<AdminDashboardFunctions>(loggerFactory),
                userRepo.Object);

            await Record.ExceptionAsync(() => sut.GetIntegrationStatus(requestMock.Object));

            loggerFactory.Entries.Should().NotBeEmpty("the function logs that it started");
            loggerFactory.AllText().Should().NotContain(t =>
                t.Contains("Relevant Env Vars") || t.Contains("Target Var") || t.Contains("[Diagnose]"));
        }
    }
}
