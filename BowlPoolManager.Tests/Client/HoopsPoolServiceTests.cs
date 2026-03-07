using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using BowlPoolManager.Client.Services;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Client
{
    /// <summary>
    /// Stub HttpMessageHandler that records the last request URI and returns a fixed response.
    /// </summary>
    internal sealed class CapturingStubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public Uri? LastRequestUri { get; private set; }

        public CapturingStubHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    public class HoopsPoolServiceTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────────

        private static (HoopsPoolService sut, CapturingStubHandler handler) Build(
            HttpStatusCode statusCode, string body)
        {
            var handler = new CapturingStubHandler(statusCode, body);
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
            return (new HoopsPoolService(httpClient), handler);
        }

        private static string SerializePools(List<HoopsPool> pools) =>
            JsonSerializer.Serialize(pools);

        private static string SerializePool(HoopsPool pool) =>
            JsonSerializer.Serialize(pool);

        // ── GetPoolsAsync — URL construction ──────────────────────────────────────

        [Fact]
        public async Task GetPoolsAsync_ShouldCallCorrectUrl_WhenNoSeasonIdProvided()
        {
            var (sut, handler) = Build(HttpStatusCode.OK, SerializePools(new List<HoopsPool>()));

            await sut.GetPoolsAsync();

            handler.LastRequestUri.Should().NotBeNull();
            handler.LastRequestUri!.PathAndQuery.Should().Be("/api/GetHoopsPools");
        }

        [Fact]
        public async Task GetPoolsAsync_ShouldIncludeSeasonIdQueryParam_WhenSeasonIdProvided()
        {
            var (sut, handler) = Build(HttpStatusCode.OK, SerializePools(new List<HoopsPool>()));

            await sut.GetPoolsAsync("season-2026");

            handler.LastRequestUri.Should().NotBeNull();
            handler.LastRequestUri!.PathAndQuery.Should().Be("/api/GetHoopsPools?seasonId=season-2026");
        }

        [Fact]
        public async Task GetPoolsAsync_ShouldNotIncludeQueryParam_WhenSeasonIdIsNull()
        {
            var (sut, handler) = Build(HttpStatusCode.OK, SerializePools(new List<HoopsPool>()));

            await sut.GetPoolsAsync(null);

            handler.LastRequestUri!.Query.Should().BeEmpty();
        }

        [Fact]
        public async Task GetPoolsAsync_ShouldNotIncludeQueryParam_WhenSeasonIdIsEmpty()
        {
            var (sut, handler) = Build(HttpStatusCode.OK, SerializePools(new List<HoopsPool>()));

            await sut.GetPoolsAsync(string.Empty);

            handler.LastRequestUri!.Query.Should().BeEmpty();
        }

        // ── GetPoolsAsync — response handling ─────────────────────────────────────

        [Fact]
        public async Task GetPoolsAsync_ShouldReturnPools_WhenApiReturnsValidJson()
        {
            var pools = new List<HoopsPool>
            {
                new() { Id = "pool-1", Name = "Pool A", SeasonId = "season-2026" },
                new() { Id = "pool-2", Name = "Pool B", SeasonId = "season-2026" }
            };
            var (sut, _) = Build(HttpStatusCode.OK, SerializePools(pools));

            var result = await sut.GetPoolsAsync("season-2026");

            result.Should().HaveCount(2);
            result.Should().Contain(p => p.Name == "Pool A");
            result.Should().Contain(p => p.Name == "Pool B");
        }

        [Fact]
        public async Task GetPoolsAsync_ShouldReturnEmptyList_WhenApiReturnsNull()
        {
            var (sut, _) = Build(HttpStatusCode.OK, "null");

            var result = await sut.GetPoolsAsync();

            result.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public async Task GetPoolsAsync_ShouldReturnEmptyList_WhenApiReturnsEmptyArray()
        {
            var (sut, _) = Build(HttpStatusCode.OK, "[]");

            var result = await sut.GetPoolsAsync();

            result.Should().BeEmpty();
        }

        // ── GetPoolAsync — client-side find by Id ─────────────────────────────────

        [Fact]
        public async Task GetPoolAsync_ShouldReturnMatchingPool_WhenIdExists()
        {
            var pools = new List<HoopsPool>
            {
                new() { Id = "pool-aaa", Name = "Alpha" },
                new() { Id = "pool-bbb", Name = "Beta" }
            };
            var (sut, _) = Build(HttpStatusCode.OK, SerializePools(pools));

            var result = await sut.GetPoolAsync("pool-bbb");

            result.Should().NotBeNull();
            result!.Name.Should().Be("Beta");
        }

        [Fact]
        public async Task GetPoolAsync_ShouldReturnNull_WhenIdDoesNotExist()
        {
            var pools = new List<HoopsPool>
            {
                new() { Id = "pool-aaa", Name = "Alpha" }
            };
            var (sut, _) = Build(HttpStatusCode.OK, SerializePools(pools));

            var result = await sut.GetPoolAsync("pool-zzz");

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetPoolAsync_ShouldReturnNull_WhenServerReturnsEmptyList()
        {
            var (sut, _) = Build(HttpStatusCode.OK, "[]");

            var result = await sut.GetPoolAsync("pool-aaa");

            result.Should().BeNull();
        }

        // ── CreatePoolAsync ────────────────────────────────────────────────────────

        [Fact]
        public async Task CreatePoolAsync_ShouldReturnPool_WhenApiReturnsSuccess()
        {
            var pool = new HoopsPool { Id = "new-pool", Name = "Test Pool", InviteCode = "CODE1" };
            var (sut, _) = Build(HttpStatusCode.OK, SerializePool(pool));

            var result = await sut.CreatePoolAsync(pool);

            result.Should().NotBeNull();
            result!.Name.Should().Be("Test Pool");
        }

        [Fact]
        public async Task CreatePoolAsync_ShouldReturnNull_WhenApiReturnsBadRequest()
        {
            var pool = new HoopsPool { Name = string.Empty };
            var (sut, _) = Build(HttpStatusCode.BadRequest, "Pool Name is required.");

            var result = await sut.CreatePoolAsync(pool);

            result.Should().BeNull();
        }

        [Fact]
        public async Task CreatePoolAsync_ShouldReturnNull_WhenApiReturnsUnauthorized()
        {
            var pool = new HoopsPool { Name = "Some Pool", InviteCode = "CODE1" };
            var (sut, _) = Build(HttpStatusCode.Unauthorized, string.Empty);

            var result = await sut.CreatePoolAsync(pool);

            result.Should().BeNull();
        }

        // ── UpdatePoolAsync ────────────────────────────────────────────────────────

        [Fact]
        public async Task UpdatePoolAsync_ShouldReturnUpdatedPool_WhenApiReturnsSuccess()
        {
            var pool = new HoopsPool { Id = "pool-123", Name = "Updated Name" };
            var (sut, _) = Build(HttpStatusCode.OK, SerializePool(pool));

            var result = await sut.UpdatePoolAsync(pool);

            result.Should().NotBeNull();
            result!.Name.Should().Be("Updated Name");
        }

        [Fact]
        public async Task UpdatePoolAsync_ShouldReturnNull_WhenApiReturnsBadRequest()
        {
            var pool = new HoopsPool { Id = "pool-123", Name = string.Empty };
            var (sut, _) = Build(HttpStatusCode.BadRequest, "Pool Name is required.");

            var result = await sut.UpdatePoolAsync(pool);

            result.Should().BeNull();
        }

        [Fact]
        public async Task UpdatePoolAsync_ShouldReturnNull_WhenApiReturnsForbidden()
        {
            var pool = new HoopsPool { Id = "pool-123", Name = "Test" };
            var (sut, _) = Build(HttpStatusCode.Forbidden, string.Empty);

            var result = await sut.UpdatePoolAsync(pool);

            result.Should().BeNull();
        }

        // ── DeletePoolAsync ────────────────────────────────────────────────────────

        [Fact]
        public async Task DeletePoolAsync_ShouldReturnTrue_WhenApiReturnsSuccess()
        {
            var (sut, _) = Build(HttpStatusCode.OK, string.Empty);

            var result = await sut.DeletePoolAsync("pool-123");

            result.Should().BeTrue();
        }

        [Fact]
        public async Task DeletePoolAsync_ShouldReturnFalse_WhenApiReturnsBadRequest()
        {
            var (sut, _) = Build(HttpStatusCode.BadRequest, string.Empty);

            var result = await sut.DeletePoolAsync(string.Empty);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task DeletePoolAsync_ShouldReturnFalse_WhenApiReturnsForbidden()
        {
            var (sut, _) = Build(HttpStatusCode.Forbidden, string.Empty);

            var result = await sut.DeletePoolAsync("pool-123");

            result.Should().BeFalse();
        }

        [Fact]
        public async Task DeletePoolAsync_ShouldCallCorrectUrl_WithPoolId()
        {
            var (sut, handler) = Build(HttpStatusCode.OK, string.Empty);

            await sut.DeletePoolAsync("pool-abc");

            handler.LastRequestUri!.PathAndQuery.Should().Be("/api/DeleteHoopsPool/pool-abc");
        }

        // ── ToggleConclusionAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task ToggleConclusionAsync_ShouldReturnPool_WhenApiReturnsSuccess()
        {
            var pool = new HoopsPool { Id = "pool-123", IsConcluded = true };
            var (sut, _) = Build(HttpStatusCode.OK, SerializePool(pool));

            var result = await sut.ToggleConclusionAsync("pool-123");

            result.Should().NotBeNull();
            result!.IsConcluded.Should().BeTrue();
        }

        [Fact]
        public async Task ToggleConclusionAsync_ShouldReturnNull_WhenApiReturnsNotFound()
        {
            var (sut, _) = Build(HttpStatusCode.NotFound, string.Empty);

            var result = await sut.ToggleConclusionAsync("pool-missing");

            result.Should().BeNull();
        }

        [Fact]
        public async Task ToggleConclusionAsync_ShouldReturnNull_WhenApiReturnsForbidden()
        {
            var (sut, _) = Build(HttpStatusCode.Forbidden, string.Empty);

            var result = await sut.ToggleConclusionAsync("pool-123");

            result.Should().BeNull();
        }

        [Fact]
        public async Task ToggleConclusionAsync_ShouldCallCorrectUrl_WithPoolId()
        {
            var pool = new HoopsPool { Id = "pool-xyz", IsConcluded = false };
            var (sut, handler) = Build(HttpStatusCode.OK, SerializePool(pool));

            await sut.ToggleConclusionAsync("pool-xyz");

            handler.LastRequestUri!.PathAndQuery.Should().Be("/api/HoopsPools/pool-xyz/ToggleConclusion");
        }
    }
}
