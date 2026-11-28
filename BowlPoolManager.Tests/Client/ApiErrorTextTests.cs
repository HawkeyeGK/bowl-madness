using System.Net;
using FluentAssertions;
using BowlPoolManager.Client.Helpers;

namespace BowlPoolManager.Tests.Client
{
    /// <summary>Issue 138: admin pages show the server's reason when a save fails.</summary>
    public class ApiErrorTextTests
    {
        private static HttpResponseMessage Response(HttpStatusCode code, string body) =>
            new(code) { Content = new StringContent(body) };

        [Fact]
        public async Task FromAsync_ShouldReturnTheServersMessage_WhenItSentOne()
        {
            var text = await ApiErrorText.FromAsync(Response(HttpStatusCode.BadRequest, "Lock Date must be in the future."));

            text.Should().Be("Lock Date must be in the future.");
        }

        [Fact]
        public async Task FromAsync_ShouldNameTheStatus_WhenTheBodyIsEmpty()
        {
            var text = await ApiErrorText.FromAsync(Response(HttpStatusCode.InternalServerError, ""));

            text.Should().Contain("500");
        }

        [Fact]
        public async Task FromAsync_ShouldNameTheStatus_WhenTheBodyIsAnHtmlPage()
        {
            var text = await ApiErrorText.FromAsync(Response(HttpStatusCode.BadGateway, "<html><body>Bad gateway</body></html>"));

            text.Should().Contain("502").And.NotContain("<html>");
        }

        [Fact]
        public async Task EnsureSuccessAsync_ShouldThrowWithTheServersMessage_WhenTheResponseFailed()
        {
            var act = () => ApiErrorText.EnsureSuccessAsync(Response(HttpStatusCode.BadRequest, "Pool Name is required."));

            (await act.Should().ThrowAsync<HttpRequestException>())
                .Where(e => e.Message == "Pool Name is required." && e.StatusCode == HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task EnsureSuccessAsync_ShouldNotThrow_WhenTheResponseSucceeded()
        {
            var act = () => ApiErrorText.EnsureSuccessAsync(Response(HttpStatusCode.OK, "{}"));

            await act.Should().NotThrowAsync();
        }
    }
}
