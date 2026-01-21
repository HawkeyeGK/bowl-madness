using Xunit;
using FluentAssertions;
using BowlPoolManager.Api.Helpers;
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
    }
}
