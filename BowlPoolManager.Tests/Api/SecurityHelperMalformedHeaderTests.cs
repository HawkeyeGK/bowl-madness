using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;
using BowlPoolManager.Api.Helpers;
using BowlPoolManager.Api.Repositories;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// A malformed x-ms-client-principal header is not a signed-in user. Static Web Apps sets the header,
    /// so this should not happen in practice, but the helper must not turn a bad header into an unhandled
    /// exception (a 500) on a protected endpoint. Rule: BUSINESS-RULES section 1 (every endpoint enforces its own checks).
    /// </summary>
    public class SecurityHelperMalformedHeaderTests
    {
        private static HttpRequestData RequestWithHeader(string value)
        {
            var ctx = new Mock<FunctionContext>().Object;
            var headers = new HttpHeadersCollection();
            headers.Add("x-ms-client-principal", value);

            var req = new Mock<HttpRequestData>(ctx);
            req.Setup(r => r.Headers).Returns(headers);
            var resp = new Mock<HttpResponseData>(ctx);
            resp.SetupProperty(r => r.StatusCode);
            req.Setup(r => r.CreateResponse()).Returns(resp.Object);
            return req.Object;
        }

        private static string Encode(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

        [Trait("Category", "KnownFailure")] // No issue yet: a malformed header throws (500) instead of counting as not signed in.
        [Theory]
        [InlineData("not base64 at all!")]
        [InlineData("====")]
        [InlineData("YWJj\u0000")]
        public void ParseSwaHeader_ShouldReturnNull_WhenHeaderIsNotBase64(string header)
        {
            var act = () => SecurityHelper.ParseSwaHeader(RequestWithHeader(header));

            act.Should().NotThrow();
            act().Should().BeNull();
        }

        [Trait("Category", "KnownFailure")] // No issue yet: a malformed header throws (500) instead of counting as not signed in.
        [Theory]
        [InlineData("not json")]
        [InlineData("[1,2,3]")]
        [InlineData("\"just a string\"")]
        public void ParseSwaHeader_ShouldReturnNull_WhenDecodedHeaderIsNotAPrincipalObject(string decoded)
        {
            var act = () => SecurityHelper.ParseSwaHeader(RequestWithHeader(Encode(decoded)));

            act.Should().NotThrow();
            act().Should().BeNull();
        }

        [Trait("Category", "KnownFailure")] // No issue yet: a malformed header throws (500) instead of counting as not signed in.
        [Fact]
        public async Task ValidateSuperAdminAsync_ShouldReturn401_WhenHeaderIsNotBase64()
        {
            var users = new Mock<IUserRepository>();

            var result = await SecurityHelper.ValidateSuperAdminAsync(RequestWithHeader("not base64 at all!"), users.Object);

            result.IsValid.Should().BeFalse();
            result.ErrorResponse!.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            users.Verify(u => u.GetUserAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void ParseSwaHeader_ShouldReadPrincipal_WhenHeaderHasExtraUnknownFields()
        {
            var header = Encode("{\"userId\":\"u1\",\"identityProvider\":\"aad\",\"userDetails\":\"x\",\"userRoles\":[\"authenticated\"],\"claims\":[{\"typ\":\"a\"}]}");

            var principal = SecurityHelper.ParseSwaHeader(RequestWithHeader(header));

            principal.Should().NotBeNull();
            principal!.UserId.Should().Be("u1");
        }
    }
}
