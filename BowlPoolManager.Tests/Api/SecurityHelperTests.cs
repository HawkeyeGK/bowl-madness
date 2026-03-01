using Xunit;
using FluentAssertions;
using BowlPoolManager.Api.Helpers;
using BowlPoolManager.Core.Domain;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Moq;
using System.Text;
using System.Text.Json;

namespace BowlPoolManager.Tests.Api
{
    public class SecurityHelperTests
    {
        [Fact]
        public void ParseSwaHeader_ShouldReturnNull_WhenHeaderIsMissing()
        {
            // Arrange
            var contextMock = new Mock<FunctionContext>();
            var requestMock = new Mock<HttpRequestData>(contextMock.Object);
            requestMock.Setup(r => r.Headers).Returns(new HttpHeadersCollection());

            // Act
            var result = SecurityHelper.ParseSwaHeader(requestMock.Object);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void ParseSwaHeader_ShouldParseValidHeader()
        {
            // Arrange
            var principal = new
            {
                userId = "user-123",
                identityProvider = "aad",
                userDetails = "user@example.com",
                userRoles = new[] { "authenticated" }
            };

            var json = JsonSerializer.Serialize(principal);
            var base64Header = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

            var headers = new HttpHeadersCollection();
            headers.Add("x-ms-client-principal", base64Header);

            var contextMock = new Mock<FunctionContext>();
            var requestMock = new Mock<HttpRequestData>(contextMock.Object);
            requestMock.Setup(r => r.Headers).Returns(headers);

            // Act
            var result = SecurityHelper.ParseSwaHeader(requestMock.Object);

            // Assert
            result.Should().NotBeNull();
            result!.UserId.Should().Be("user-123");
            result.IdentityProvider.Should().Be("aad");
            result.UserDetails.Should().Be("user@example.com");
        }

        [Fact]
        public void ParseSwaHeader_ShouldReturnNull_WhenHeaderIsEmptyString()
        {
            // Arrange
            var headers = new HttpHeadersCollection();
            headers.Add("x-ms-client-principal", "");

            var contextMock = new Mock<FunctionContext>();
            var requestMock = new Mock<HttpRequestData>(contextMock.Object);
            requestMock.Setup(r => r.Headers).Returns(headers);

            // Act
            var result = SecurityHelper.ParseSwaHeader(requestMock.Object);

            // Assert
            result.Should().BeNull();
        }

        #region IsAdmin Tests

        [Fact]
        public void IsAdmin_ShouldReturnTrue_ForSuperAdmin()
        {
            // Arrange
            var user = new UserProfile { AppRole = BowlPoolManager.Core.Constants.Roles.SuperAdmin };

            // Act
            var result = SecurityHelper.IsAdmin(user);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsAdmin_ShouldReturnTrue_ForAdmin()
        {
            // Arrange
            var user = new UserProfile { AppRole = BowlPoolManager.Core.Constants.Roles.Admin };

            // Act
            var result = SecurityHelper.IsAdmin(user);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsAdmin_ShouldReturnFalse_ForPlayer()
        {
            // Arrange
            var user = new UserProfile { AppRole = BowlPoolManager.Core.Constants.Roles.Player };

            // Act
            var result = SecurityHelper.IsAdmin(user);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsAdmin_ShouldReturnFalse_ForNullUser()
        {
            // Act
            var result = SecurityHelper.IsAdmin(null);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsAdmin_ShouldReturnFalse_ForUserWithEmptyRole()
        {
            // Arrange
            var user = new UserProfile { AppRole = string.Empty };

            // Act
            var result = SecurityHelper.IsAdmin(user);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region ValidateSuperAdminAsync Tests

        // CreateResponse(HttpStatusCode) is an extension method (HttpRequestDataExtensions) and cannot
        // be mocked or verified via Moq. Instead we mock the abstract no-args CreateResponse() and use
        // SetupProperty on StatusCode so the extension method can write through to the mock object.
        private static (HttpRequestData req, HttpResponseData resp) BuildRequest(FunctionContext ctx, string? swaHeaderValue)
        {
            var headers = new HttpHeadersCollection();
            if (swaHeaderValue != null)
                headers.Add("x-ms-client-principal", swaHeaderValue);

            var requestMock = new Mock<HttpRequestData>(ctx);
            requestMock.Setup(r => r.Headers).Returns(headers);

            var responseMock = new Mock<HttpResponseData>(ctx);
            responseMock.SetupProperty(r => r.StatusCode);
            requestMock.Setup(r => r.CreateResponse()).Returns(responseMock.Object);

            return (requestMock.Object, responseMock.Object);
        }

        private static string EncodePrincipal(string userId) =>
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(new { userId, userDetails = "u@test.com", identityProvider = "aad", userRoles = new[] { "authenticated" } })));

        [Fact]
        public async Task ValidateSuperAdminAsync_ShouldReturn401_WhenHeaderMissing()
        {
            var ctx = new Mock<FunctionContext>().Object;
            var (req, resp) = BuildRequest(ctx, swaHeaderValue: null);
            var repoMock = new Mock<BowlPoolManager.Api.Repositories.IUserRepository>();

            var result = await SecurityHelper.ValidateSuperAdminAsync(req, repoMock.Object);

            result.IsValid.Should().BeFalse();
            result.ErrorResponse.Should().NotBeNull();
            resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task ValidateSuperAdminAsync_ShouldReturn401_WhenUserIdIsEmpty()
        {
            var ctx = new Mock<FunctionContext>().Object;
            var (req, resp) = BuildRequest(ctx, swaHeaderValue: EncodePrincipal(userId: ""));
            var repoMock = new Mock<BowlPoolManager.Api.Repositories.IUserRepository>();

            var result = await SecurityHelper.ValidateSuperAdminAsync(req, repoMock.Object);

            result.IsValid.Should().BeFalse();
            resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task ValidateSuperAdminAsync_ShouldReturn403_WhenUserNotFoundInDb()
        {
            var ctx = new Mock<FunctionContext>().Object;
            var (req, resp) = BuildRequest(ctx, swaHeaderValue: EncodePrincipal("user-99"));
            var repoMock = new Mock<BowlPoolManager.Api.Repositories.IUserRepository>();
            repoMock.Setup(r => r.GetUserAsync("user-99")).ReturnsAsync((UserProfile?)null);

            var result = await SecurityHelper.ValidateSuperAdminAsync(req, repoMock.Object);

            result.IsValid.Should().BeFalse();
            resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task ValidateSuperAdminAsync_ShouldReturn403_WhenUserIsAdmin()
        {
            var ctx = new Mock<FunctionContext>().Object;
            var (req, resp) = BuildRequest(ctx, swaHeaderValue: EncodePrincipal("user-admin"));
            var repoMock = new Mock<BowlPoolManager.Api.Repositories.IUserRepository>();
            repoMock.Setup(r => r.GetUserAsync("user-admin"))
                    .ReturnsAsync(new UserProfile { AppRole = BowlPoolManager.Core.Constants.Roles.Admin });

            var result = await SecurityHelper.ValidateSuperAdminAsync(req, repoMock.Object);

            result.IsValid.Should().BeFalse();
            resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task ValidateSuperAdminAsync_ShouldReturnSuccess_WhenUserIsSuperAdmin()
        {
            var ctx = new Mock<FunctionContext>().Object;
            var (req, _) = BuildRequest(ctx, swaHeaderValue: EncodePrincipal("user-sa"));
            var repoMock = new Mock<BowlPoolManager.Api.Repositories.IUserRepository>();
            repoMock.Setup(r => r.GetUserAsync("user-sa"))
                    .ReturnsAsync(new UserProfile { AppRole = BowlPoolManager.Core.Constants.Roles.SuperAdmin });

            var result = await SecurityHelper.ValidateSuperAdminAsync(req, repoMock.Object);

            result.IsValid.Should().BeTrue();
            result.ErrorResponse.Should().BeNull();
        }

        #endregion
    }
}
