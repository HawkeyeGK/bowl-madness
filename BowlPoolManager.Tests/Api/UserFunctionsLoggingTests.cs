using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using BowlPoolManager.Api.Functions;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// Checks what UserFunctions writes to the log. The success path writes the response with
    /// WriteAsJsonAsync, which needs a real response stream and is not exercised here; the log
    /// line under test is written before that call.
    /// </summary>
    public class UserFunctionsLoggingTests
    {
        private const string Email = "player@example.com";
        private const string UserId = "user-123";

        private static HttpRequestData BuildRequest(FunctionContext ctx, string swaHeaderValue)
        {
            var headers = new HttpHeadersCollection();
            headers.Add("x-ms-client-principal", swaHeaderValue);

            var requestMock = new Mock<HttpRequestData>(ctx);
            requestMock.Setup(r => r.Headers).Returns(headers);

            var responseMock = new Mock<HttpResponseData>(ctx);
            responseMock.SetupProperty(r => r.StatusCode);
            responseMock.Setup(r => r.Body).Returns(new MemoryStream());
            requestMock.Setup(r => r.CreateResponse()).Returns(responseMock.Object);

            return requestMock.Object;
        }

        private static string EncodePrincipal(string userId, string userDetails) =>
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(new
                    {
                        userId,
                        userDetails,
                        identityProvider = "aad",
                        userRoles = new[] { "authenticated" }
                    })));

        [Fact]
        public async Task GetMe_ShouldNotLogEmail_WhenCreatingNewUser()
        {
            var ctx = new Mock<FunctionContext>().Object;
            var req = BuildRequest(ctx, EncodePrincipal(UserId, Email));

            var userRepo = new Mock<IUserRepository>();
            userRepo.Setup(r => r.GetUserAsync(It.IsAny<string>())).ReturnsAsync((UserProfile?)null);
            userRepo.Setup(r => r.UpsertUserAsync(It.IsAny<UserProfile>())).Returns(Task.CompletedTask);

            var loggerFactory = new CapturingLoggerFactory();
            var sut = new UserFunctions(loggerFactory, userRepo.Object, new ConfigurationBuilder().Build());

            await Record.ExceptionAsync(() => sut.GetMe(req));

            loggerFactory.AllText().Should().NotContain(t => t.Contains(Email));
        }

        [Fact]
        public async Task GetMe_ShouldLogUserId_WhenCreatingNewUser()
        {
            var ctx = new Mock<FunctionContext>().Object;
            var req = BuildRequest(ctx, EncodePrincipal(UserId, Email));

            var userRepo = new Mock<IUserRepository>();
            userRepo.Setup(r => r.GetUserAsync(It.IsAny<string>())).ReturnsAsync((UserProfile?)null);
            userRepo.Setup(r => r.UpsertUserAsync(It.IsAny<UserProfile>())).Returns(Task.CompletedTask);

            var loggerFactory = new CapturingLoggerFactory();
            var sut = new UserFunctions(loggerFactory, userRepo.Object, new ConfigurationBuilder().Build());

            await Record.ExceptionAsync(() => sut.GetMe(req));

            loggerFactory.Entries.Should().Contain(e =>
                e.Level == LogLevel.Information && e.Message.Contains(UserId));
        }
    }
}
