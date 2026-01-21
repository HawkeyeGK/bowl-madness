using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using BowlPoolManager.Api.Functions;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using System.Net;
using System.Text;
using System.Text.Json;

namespace BowlPoolManager.Tests.Api.Functions
{
    public class BackupFunctionsTests
    {
        private readonly Mock<ILoggerFactory> _mockLoggerFactory;
        private readonly Mock<ILogger<BackupFunctions>> _mockLogger;
        private readonly Mock<IEntryRepository> _mockEntryRepo;
        private readonly Mock<IGameRepository> _mockGameRepo;
        private readonly Mock<IUserRepository> _mockUserRepo;
        private readonly Mock<IPoolRepository> _mockPoolRepo;
        private readonly BackupFunctions _functions;

        public BackupFunctionsTests()
        {
            _mockLoggerFactory = new Mock<ILoggerFactory>();
            _mockLogger = new Mock<ILogger<BackupFunctions>>();
            _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_mockLogger.Object);

            _mockEntryRepo = new Mock<IEntryRepository>();
            _mockGameRepo = new Mock<IGameRepository>();
            _mockUserRepo = new Mock<IUserRepository>();
            _mockPoolRepo = new Mock<IPoolRepository>();

            _functions = new BackupFunctions(
                _mockLoggerFactory.Object, 
                _mockEntryRepo.Object, 
                _mockGameRepo.Object, 
                _mockUserRepo.Object, 
                _mockPoolRepo.Object
            );
        }

        [Fact]
        public async Task GetBackupData_WhenNoAuthHeader_ReturnsUnauthorized()
        {
            // Arrange
            var contextMock = new Mock<FunctionContext>();
            var requestMock = new Mock<HttpRequestData>(contextMock.Object);
            requestMock.Setup(r => r.Headers).Returns(new HttpHeadersCollection());
            
            // Mock CreateResponse
            requestMock.Setup(r => r.CreateResponse()).Returns(() => {
                var resp = new Mock<HttpResponseData>(contextMock.Object);
                resp.SetupProperty(r => r.StatusCode, HttpStatusCode.Unauthorized);
                resp.SetupProperty(r => r.Headers, new HttpHeadersCollection());
                return resp.Object;
            });

            // Act
            var response = await _functions.GetBackupData(requestMock.Object);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetBackupData_WhenUserNotSuperAdmin_ReturnsForbidden()
        {
            // Arrange
            var userId = "user123";
            var contextMock = new Mock<FunctionContext>();
            var requestMock = CreateAuthenticatedRequest(contextMock, userId);

            // Mock User Repo to return standard user
            _mockUserRepo.Setup(r => r.GetUserAsync(userId))
                .ReturnsAsync(new UserProfile { Id = userId, AppRole = "User" }); // Not SuperAdmin

            // Mock CreateResponse
            requestMock.Setup(r => r.CreateResponse()).Returns(() => {
                var resp = new Mock<HttpResponseData>(contextMock.Object);
                resp.SetupProperty(r => r.StatusCode, HttpStatusCode.Forbidden);
                resp.SetupProperty(r => r.Headers, new HttpHeadersCollection());
                return resp.Object;
            });

            // Act
            var response = await _functions.GetBackupData(requestMock.Object);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetBackupData_WhenSuperAdmin_ReturnsFile()
        {
            // Arrange
            var userId = "admin123";
            var contextMock = new Mock<FunctionContext>();
            var requestMock = CreateAuthenticatedRequest(contextMock, userId);

            // Mock User Repo to return SuperAdmin
            _mockUserRepo.Setup(r => r.GetUserAsync(userId))
                .ReturnsAsync(new UserProfile { Id = userId, AppRole = Constants.Roles.SuperAdmin });

            // Mock Data Querying
            _mockEntryRepo.Setup(r => r.GetEntriesAsync(It.IsAny<string?>())).ReturnsAsync(new List<BracketEntry>());
            _mockGameRepo.Setup(r => r.GetGamesAsync()).ReturnsAsync(new List<BowlGame>());
            _mockPoolRepo.Setup(r => r.GetPoolsAsync()).ReturnsAsync(new List<BowlPool>());
            _mockUserRepo.Setup(r => r.GetUsersAsync()).ReturnsAsync(new List<UserProfile>());

            // Mock Response Writing
            // Writing to Body is typically via Body property which is a stream.
            // We need to ensure the mocked HttpResponseData has a writable Body stream.
            var memoryStream = new MemoryStream();
            requestMock.Setup(r => r.CreateResponse()).Returns(() => {
                var resp = new Mock<HttpResponseData>(contextMock.Object);
                resp.SetupProperty(r => r.StatusCode, HttpStatusCode.OK);
                resp.SetupProperty(r => r.Headers, new HttpHeadersCollection());
                resp.Setup(r => r.Body).Returns(memoryStream);
                // Setup WriteStringAsync mock implicitly by stream availability? 
                // Extension methods like WriteStringAsync behave differently. 
                // Wait, WriteStringAsync writes to the Body stream.
                return resp.Object;
            });

            // Act
            var response = await _functions.GetBackupData(requestMock.Object);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.First(h => h.Key == "Content-Type").Value.First().Should().Be("application/json");
        }

        private Mock<HttpRequestData> CreateAuthenticatedRequest(Mock<FunctionContext> contextMock, string userId)
        {
            var requestMock = new Mock<HttpRequestData>(contextMock.Object);
            var headers = new HttpHeadersCollection();
            
            var clientPrincipal = new
            {
                userId = userId,
                identityProvider = "test",
                userDetails = "test@example.com",
                userRoles = new[] { "anonymous", "authenticated" }
            };

            var json = JsonSerializer.Serialize(clientPrincipal);
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            headers.Add("x-ms-client-principal", base64);

            requestMock.Setup(r => r.Headers).Returns(headers);
            return requestMock;
        }
    }
}
