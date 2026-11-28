using System.Net;
using FluentAssertions;
using BowlPoolManager.Client.Services;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Client
{
    /// <summary>
    /// Issue 138: the football pool writes and the season save no longer fail silently. A refused request throws
    /// HttpRequestException carrying the server's message, which the admin pages show.
    /// </summary>
    public class PoolAndSeasonServiceErrorTests
    {
        private static HttpClient Client(HttpStatusCode code, string body, out CapturingStubHandler handler)
        {
            handler = new CapturingStubHandler(code, body);
            return new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        }

        private static BowlPool Pool() => new() { Id = "pool-1", SeasonId = "season-1", Name = "Family", InviteCode = "code" };

        [Fact]
        public async Task CreatePoolAsync_ShouldThrowWithTheServersMessage_WhenRefused()
        {
            var sut = new PoolService(Client(HttpStatusCode.BadRequest, "Lock Date must be in the future.", out _));

            var act = () => sut.CreatePoolAsync(Pool());

            (await act.Should().ThrowAsync<HttpRequestException>()).WithMessage("Lock Date must be in the future.");
        }

        [Fact]
        public async Task UpdatePoolAsync_ShouldThrowWithTheServersMessage_WhenRefused()
        {
            var sut = new PoolService(Client(HttpStatusCode.BadRequest, "Pool Name is required.", out _));

            var act = () => sut.UpdatePoolAsync(Pool());

            (await act.Should().ThrowAsync<HttpRequestException>()).WithMessage("Pool Name is required.");
        }

        [Fact]
        public async Task DeletePoolAsync_ShouldThrow_WhenRefused()
        {
            var sut = new PoolService(Client(HttpStatusCode.Forbidden, "", out _));

            var act = () => sut.DeletePoolAsync("pool-1");

            (await act.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task ToggleConclusionAsync_ShouldThrow_WhenRefused()
        {
            var sut = new PoolService(Client(HttpStatusCode.NotFound, "", out _));

            var act = () => sut.ToggleConclusionAsync("pool-1");

            await act.Should().ThrowAsync<HttpRequestException>();
        }

        [Fact]
        public async Task ArchivePoolAsync_ShouldThrowWithTheServersMessage_WhenRefused()
        {
            var sut = new PoolService(Client(HttpStatusCode.BadRequest, "Pool must be concluded before archiving.", out _));

            var act = () => sut.ArchivePoolAsync("pool-1");

            (await act.Should().ThrowAsync<HttpRequestException>()).WithMessage("Pool must be concluded before archiving.");
        }

        [Fact]
        public async Task DeletePoolAsync_ShouldReturnTrue_WhenTheServerAccepts()
        {
            var sut = new PoolService(Client(HttpStatusCode.OK, "", out var handler));

            (await sut.DeletePoolAsync("pool-1")).Should().BeTrue();
            handler.LastRequestUri!.AbsolutePath.Should().Be("/api/DeletePool/pool-1");
        }

        [Fact]
        public async Task UpsertSeasonAsync_ShouldThrowWithTheServersMessage_WhenRefused()
        {
            var sut = new SeasonService(Client(HttpStatusCode.BadRequest, "Season ID (Year) is required.", out _));

            var act = () => sut.UpsertSeasonAsync(new Season());

            (await act.Should().ThrowAsync<HttpRequestException>()).WithMessage("Season ID (Year) is required.");
        }

        [Fact]
        public async Task GetCurrentSeasonAsync_ShouldPickTheNewestYear_WhenNoneIsMarkedCurrent()
        {
            var body = "[{\"id\":\"ffff\",\"seasonId\":\"2023\"},{\"id\":\"0000\",\"seasonId\":\"2025\"}]";
            var sut = new SeasonService(Client(HttpStatusCode.OK, body, out _));

            (await sut.GetCurrentSeasonAsync())!.SeasonId.Should().Be("2025");
        }
    }
}
