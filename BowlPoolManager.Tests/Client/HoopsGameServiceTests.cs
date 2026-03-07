using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using BowlPoolManager.Client.Services;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Tests.Client
{
    public class HoopsGameServiceTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────────

        private static (HoopsGameService sut, CapturingStubHandler handler) Build(
            HttpStatusCode statusCode, string body)
        {
            var handler = new CapturingStubHandler(statusCode, body);
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
            return (new HoopsGameService(httpClient), handler);
        }

        private static string SerializeGames(List<HoopsGame> games) =>
            JsonSerializer.Serialize(games);

        private static string SerializeGame(HoopsGame game) =>
            JsonSerializer.Serialize(game);

        private static BracketGenerationRequest StandardRequest() => new()
        {
            PoolId = "pool-1",
            SeasonId = "season-2026",
            Regions = new List<string> { "East", "West", "South", "Midwest" },
            FinalFourPairings = new List<List<string>>
            {
                new() { "East", "West" },
                new() { "South", "Midwest" }
            }
        };

        // ── GetGamesAsync — URL construction ──────────────────────────────────────

        [Fact]
        public async Task GetGamesAsync_ShouldCallCorrectUrl_WithPoolId()
        {
            var (sut, handler) = Build(HttpStatusCode.OK, SerializeGames(new List<HoopsGame>()));

            await sut.GetGamesAsync("pool-abc");

            handler.LastRequestUri.Should().NotBeNull();
            handler.LastRequestUri!.PathAndQuery.Should().Be("/api/GetHoopsGames?poolId=pool-abc");
        }

        [Fact]
        public async Task GetGamesAsync_ShouldIncludePoolIdInQueryString_ExactlyAsProvided()
        {
            var (sut, handler) = Build(HttpStatusCode.OK, SerializeGames(new List<HoopsGame>()));

            await sut.GetGamesAsync("pool-xyz-123");

            handler.LastRequestUri!.Query.Should().Contain("poolId=pool-xyz-123");
        }

        // ── GetGamesAsync — response handling ─────────────────────────────────────

        [Fact]
        public async Task GetGamesAsync_ShouldReturnGames_WhenApiReturnsValidJson()
        {
            var games = new List<HoopsGame>
            {
                new() { Id = "game-1", SeasonId = "season-2026", Round = TournamentRound.RoundOf64, Region = "East" },
                new() { Id = "game-2", SeasonId = "season-2026", Round = TournamentRound.RoundOf64, Region = "West" }
            };
            var (sut, _) = Build(HttpStatusCode.OK, SerializeGames(games));

            var result = await sut.GetGamesAsync("pool-1");

            result.Should().HaveCount(2);
            result.Should().Contain(g => g.Id == "game-1");
            result.Should().Contain(g => g.Id == "game-2");
        }

        [Fact]
        public async Task GetGamesAsync_ShouldReturnEmptyList_WhenApiReturnsNull()
        {
            var (sut, _) = Build(HttpStatusCode.OK, "null");

            var result = await sut.GetGamesAsync("pool-1");

            result.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public async Task GetGamesAsync_ShouldReturnEmptyList_WhenApiReturnsEmptyArray()
        {
            var (sut, _) = Build(HttpStatusCode.OK, "[]");

            var result = await sut.GetGamesAsync("pool-1");

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetGamesAsync_ShouldReturnEmptyList_WhenApiReturnsNotFound()
        {
            var (sut, _) = Build(HttpStatusCode.NotFound, string.Empty);

            var result = await sut.GetGamesAsync("pool-1");

            result.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public async Task GetGamesAsync_ShouldReturnEmptyList_WhenApiReturnsInternalServerError()
        {
            var (sut, _) = Build(HttpStatusCode.InternalServerError, string.Empty);

            var result = await sut.GetGamesAsync("pool-1");

            result.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public async Task GetGamesAsync_ShouldNotThrow_WhenApiThrows()
        {
            var throwingHandler = new ThrowingHttpMessageHandler();
            var httpClient = new HttpClient(throwingHandler) { BaseAddress = new Uri("https://localhost/") };
            var sut = new HoopsGameService(httpClient);

            var act = () => sut.GetGamesAsync("pool-1");

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task GetGamesAsync_ShouldReturnEmptyList_WhenApiThrows()
        {
            var throwingHandler = new ThrowingHttpMessageHandler();
            var httpClient = new HttpClient(throwingHandler) { BaseAddress = new Uri("https://localhost/") };
            var sut = new HoopsGameService(httpClient);

            var result = await sut.GetGamesAsync("pool-1");

            result.Should().NotBeNull().And.BeEmpty();
        }

        // ── GenerateBracketAsync — URL construction ────────────────────────────────

        [Fact]
        public async Task GenerateBracketAsync_ShouldCallCorrectUrl()
        {
            var games = new List<HoopsGame> { new() { Id = "game-1" } };
            var (sut, handler) = Build(HttpStatusCode.OK, SerializeGames(games));

            await sut.GenerateBracketAsync(StandardRequest());

            handler.LastRequestUri.Should().NotBeNull();
            handler.LastRequestUri!.PathAndQuery.Should().Be("/api/GenerateBracket");
        }

        // ── GenerateBracketAsync — response handling ──────────────────────────────

        [Fact]
        public async Task GenerateBracketAsync_ShouldReturnGames_WhenApiReturnsSuccess()
        {
            var games = new List<HoopsGame>
            {
                new() { Id = "game-1", Round = TournamentRound.NationalChampionship },
                new() { Id = "game-2", Round = TournamentRound.FinalFour }
            };
            var (sut, _) = Build(HttpStatusCode.OK, SerializeGames(games));

            var result = await sut.GenerateBracketAsync(StandardRequest());

            result.Should().NotBeNull();
            result!.Should().HaveCount(2);
            result.Should().Contain(g => g.Id == "game-1");
        }

        [Fact]
        public async Task GenerateBracketAsync_ShouldReturnNull_WhenApiReturnsBadRequest()
        {
            var (sut, _) = Build(HttpStatusCode.BadRequest, "Validation error.");

            var result = await sut.GenerateBracketAsync(StandardRequest());

            result.Should().BeNull();
        }

        [Fact]
        public async Task GenerateBracketAsync_ShouldReturnNull_WhenApiReturnsUnauthorized()
        {
            var (sut, _) = Build(HttpStatusCode.Unauthorized, string.Empty);

            var result = await sut.GenerateBracketAsync(StandardRequest());

            result.Should().BeNull();
        }

        [Fact]
        public async Task GenerateBracketAsync_ShouldReturnNull_WhenApiReturnsForbidden()
        {
            var (sut, _) = Build(HttpStatusCode.Forbidden, string.Empty);

            var result = await sut.GenerateBracketAsync(StandardRequest());

            result.Should().BeNull();
        }

        [Fact]
        public async Task GenerateBracketAsync_ShouldReturnNull_WhenApiReturnsInternalServerError()
        {
            var (sut, _) = Build(HttpStatusCode.InternalServerError, string.Empty);

            var result = await sut.GenerateBracketAsync(StandardRequest());

            result.Should().BeNull();
        }

        [Fact]
        public async Task GenerateBracketAsync_ShouldNotThrow_WhenApiThrows()
        {
            var throwingHandler = new ThrowingHttpMessageHandler();
            var httpClient = new HttpClient(throwingHandler) { BaseAddress = new Uri("https://localhost/") };
            var sut = new HoopsGameService(httpClient);

            var act = () => sut.GenerateBracketAsync(StandardRequest());

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task GenerateBracketAsync_ShouldReturnNull_WhenApiThrows()
        {
            var throwingHandler = new ThrowingHttpMessageHandler();
            var httpClient = new HttpClient(throwingHandler) { BaseAddress = new Uri("https://localhost/") };
            var sut = new HoopsGameService(httpClient);

            var result = await sut.GenerateBracketAsync(StandardRequest());

            result.Should().BeNull();
        }

        // ── UpdateGameAsync — URL construction ────────────────────────────────────

        [Fact]
        public async Task UpdateGameAsync_ShouldCallCorrectUrl()
        {
            var game = new HoopsGame { Id = "game-123", SeasonId = "season-2026" };
            var (sut, handler) = Build(HttpStatusCode.OK, SerializeGame(game));

            await sut.UpdateGameAsync(game);

            handler.LastRequestUri.Should().NotBeNull();
            handler.LastRequestUri!.PathAndQuery.Should().Be("/api/UpdateHoopsGame");
        }

        // ── UpdateGameAsync — response handling ────────────────────────────────────

        [Fact]
        public async Task UpdateGameAsync_ShouldReturnUpdatedGame_WhenApiReturnsSuccess()
        {
            var game = new HoopsGame
            {
                Id = "game-123",
                SeasonId = "season-2026",
                Round = TournamentRound.Elite8,
                Region = "East"
            };
            var (sut, _) = Build(HttpStatusCode.OK, SerializeGame(game));

            var result = await sut.UpdateGameAsync(game);

            result.Should().NotBeNull();
            result!.Id.Should().Be("game-123");
            result.Region.Should().Be("East");
        }

        [Fact]
        public async Task UpdateGameAsync_ShouldReturnNull_WhenApiReturnsBadRequest()
        {
            var game = new HoopsGame { Id = "game-123" };
            var (sut, _) = Build(HttpStatusCode.BadRequest, "Game not found.");

            var result = await sut.UpdateGameAsync(game);

            result.Should().BeNull();
        }

        [Fact]
        public async Task UpdateGameAsync_ShouldReturnNull_WhenApiReturnsUnauthorized()
        {
            var game = new HoopsGame { Id = "game-123" };
            var (sut, _) = Build(HttpStatusCode.Unauthorized, string.Empty);

            var result = await sut.UpdateGameAsync(game);

            result.Should().BeNull();
        }

        [Fact]
        public async Task UpdateGameAsync_ShouldReturnNull_WhenApiReturnsForbidden()
        {
            var game = new HoopsGame { Id = "game-123" };
            var (sut, _) = Build(HttpStatusCode.Forbidden, string.Empty);

            var result = await sut.UpdateGameAsync(game);

            result.Should().BeNull();
        }

        [Fact]
        public async Task UpdateGameAsync_ShouldReturnNull_WhenApiReturnsNotFound()
        {
            var game = new HoopsGame { Id = "game-999" };
            var (sut, _) = Build(HttpStatusCode.NotFound, string.Empty);

            var result = await sut.UpdateGameAsync(game);

            result.Should().BeNull();
        }

        [Fact]
        public async Task UpdateGameAsync_ShouldNotThrow_WhenApiThrows()
        {
            var throwingHandler = new ThrowingHttpMessageHandler();
            var httpClient = new HttpClient(throwingHandler) { BaseAddress = new Uri("https://localhost/") };
            var sut = new HoopsGameService(httpClient);

            var act = () => sut.UpdateGameAsync(new HoopsGame { Id = "game-123" });

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task UpdateGameAsync_ShouldReturnNull_WhenApiThrows()
        {
            var throwingHandler = new ThrowingHttpMessageHandler();
            var httpClient = new HttpClient(throwingHandler) { BaseAddress = new Uri("https://localhost/") };
            var sut = new HoopsGameService(httpClient);

            var result = await sut.UpdateGameAsync(new HoopsGame { Id = "game-123" });

            result.Should().BeNull();
        }

        // ── SaveTeamAssignmentsAsync — URL construction ───────────────────────────

        [Fact]
        public async Task SaveTeamAssignmentsAsync_ShouldCallCorrectUrl()
        {
            var games = new List<HoopsGame> { new() { Id = "game-1" } };
            var (sut, handler) = Build(HttpStatusCode.OK, string.Empty);

            await sut.SaveTeamAssignmentsAsync(games);

            handler.LastRequestUri.Should().NotBeNull();
            handler.LastRequestUri!.PathAndQuery.Should().Be("/api/SaveHoopsTeamAssignments");
        }

        // ── SaveTeamAssignmentsAsync — response handling ──────────────────────────

        [Fact]
        public async Task SaveTeamAssignmentsAsync_ShouldReturnTrue_WhenApiReturnsOk()
        {
            var games = new List<HoopsGame> { new() { Id = "game-1" } };
            var (sut, _) = Build(HttpStatusCode.OK, string.Empty);

            var result = await sut.SaveTeamAssignmentsAsync(games);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task SaveTeamAssignmentsAsync_ShouldReturnFalse_WhenApiReturnsBadRequest()
        {
            var games = new List<HoopsGame> { new() { Id = "game-1" } };
            var (sut, _) = Build(HttpStatusCode.BadRequest, string.Empty);

            var result = await sut.SaveTeamAssignmentsAsync(games);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task SaveTeamAssignmentsAsync_ShouldReturnFalse_WhenApiReturnsUnauthorized()
        {
            var games = new List<HoopsGame> { new() { Id = "game-1" } };
            var (sut, _) = Build(HttpStatusCode.Unauthorized, string.Empty);

            var result = await sut.SaveTeamAssignmentsAsync(games);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task SaveTeamAssignmentsAsync_ShouldReturnFalse_WhenApiReturnsInternalServerError()
        {
            var games = new List<HoopsGame> { new() { Id = "game-1" } };
            var (sut, _) = Build(HttpStatusCode.InternalServerError, string.Empty);

            var result = await sut.SaveTeamAssignmentsAsync(games);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task SaveTeamAssignmentsAsync_ShouldNotThrow_WhenApiThrows()
        {
            var throwingHandler = new ThrowingHttpMessageHandler();
            var httpClient = new HttpClient(throwingHandler) { BaseAddress = new Uri("https://localhost/") };
            var sut = new HoopsGameService(httpClient);
            var games = new List<HoopsGame> { new() { Id = "game-1" } };

            var act = () => sut.SaveTeamAssignmentsAsync(games);

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task SaveTeamAssignmentsAsync_ShouldReturnFalse_WhenApiThrows()
        {
            var throwingHandler = new ThrowingHttpMessageHandler();
            var httpClient = new HttpClient(throwingHandler) { BaseAddress = new Uri("https://localhost/") };
            var sut = new HoopsGameService(httpClient);
            var games = new List<HoopsGame> { new() { Id = "game-1" } };

            var result = await sut.SaveTeamAssignmentsAsync(games);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task SaveTeamAssignmentsAsync_ShouldSerializeGameList_InRequestBody()
        {
            var games = new List<HoopsGame>
            {
                new() { Id = "game-1", SeasonId = "season-2026", Round = TournamentRound.RoundOf64 },
                new() { Id = "game-2", SeasonId = "season-2026", Round = TournamentRound.FirstFour }
            };
            var bodyHandler = new BodyCapturingStubHandler(HttpStatusCode.OK, string.Empty);
            var httpClient = new HttpClient(bodyHandler) { BaseAddress = new Uri("https://localhost/") };
            var sut = new HoopsGameService(httpClient);

            await sut.SaveTeamAssignmentsAsync(games);

            bodyHandler.LastRequestBody.Should().NotBeNullOrEmpty();
            bodyHandler.LastRequestBody.Should().Contain("game-1");
            bodyHandler.LastRequestBody.Should().Contain("game-2");
        }

        // ── Nested helpers ────────────────────────────────────────────────────────

        private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken) =>
                throw new HttpRequestException("Simulated network failure");
        }

        private sealed class BodyCapturingStubHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _statusCode;
            private readonly string _responseBody;

            public string? LastRequestBody { get; private set; }

            public BodyCapturingStubHandler(HttpStatusCode statusCode, string responseBody)
            {
                _statusCode = statusCode;
                _responseBody = responseBody;
            }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                if (request.Content != null)
                    LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

                var response = new HttpResponseMessage(_statusCode)
                {
                    Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
                };
                return response;
            }
        }
    }
}
