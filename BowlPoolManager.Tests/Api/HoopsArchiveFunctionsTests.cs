using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BowlPoolManager.Api.Functions;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// Tests for HoopsArchiveFunctions covering early-exit guard paths and archive ID
    /// construction. The success path (which calls WriteAsJsonAsync) requires a real response
    /// body stream and is excluded per project convention (HTTP wiring is not tested here;
    /// the business logic delegated from that path — ScoringEngine, PointsPerRound hydration —
    /// is covered by ScoringEngineIScorableTests and HoopsWhatIfScoringEngineTests).
    /// </summary>
    public class HoopsArchiveFunctionsTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────────

        private static (HttpRequestData req, HttpResponseData resp) BuildRequest(
            FunctionContext ctx, string? swaHeaderValue)
        {
            var headers = new HttpHeadersCollection();
            if (swaHeaderValue != null)
                headers.Add("x-ms-client-principal", swaHeaderValue);

            var requestMock = new Mock<HttpRequestData>(ctx);
            requestMock.Setup(r => r.Headers).Returns(headers);

            var responseMock = new Mock<HttpResponseData>(ctx);
            responseMock.SetupProperty(r => r.StatusCode);
            responseMock.Setup(r => r.Body).Returns(new MemoryStream());
            requestMock.Setup(r => r.CreateResponse()).Returns(responseMock.Object);

            return (requestMock.Object, responseMock.Object);
        }

        private static string EncodeSuperAdminPrincipal(string userId) =>
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(new
                    {
                        userId,
                        userDetails = "admin@test.com",
                        identityProvider = "aad",
                        userRoles = new[] { "authenticated" }
                    })));

        private static HoopsArchiveFunctions BuildFunctions(
            Mock<IHoopsPoolRepository> poolRepo,
            Mock<IHoopsGameRepository> gameRepo,
            Mock<IHoopsEntryRepository> entryRepo,
            Mock<IArchiveRepository> archiveRepo,
            Mock<IUserRepository> userRepo)
        {
            var loggerFactory = new NullLoggerFactory();
            return new HoopsArchiveFunctions(
                loggerFactory,
                poolRepo.Object,
                gameRepo.Object,
                entryRepo.Object,
                archiveRepo.Object,
                userRepo.Object);
        }

        // ── ArchiveHoopsPool — auth guard ─────────────────────────────────────────

        [Fact]
        public async Task ArchiveHoopsPool_ShouldReturn401_WhenAuthHeaderMissing()
        {
            var ctx = new Mock<FunctionContext>().Object;
            var (req, resp) = BuildRequest(ctx, swaHeaderValue: null);

            var userRepo = new Mock<IUserRepository>();
            var sut = BuildFunctions(
                new Mock<IHoopsPoolRepository>(),
                new Mock<IHoopsGameRepository>(),
                new Mock<IHoopsEntryRepository>(),
                new Mock<IArchiveRepository>(),
                userRepo);

            var result = await sut.ArchiveHoopsPool(req, "pool-abc");

            result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task ArchiveHoopsPool_ShouldReturn403_WhenUserIsNotSuperAdmin()
        {
            var ctx = new Mock<FunctionContext>().Object;
            var (req, resp) = BuildRequest(ctx, swaHeaderValue: EncodeSuperAdminPrincipal("user-admin"));

            var userRepo = new Mock<IUserRepository>();
            userRepo
                .Setup(r => r.GetUserAsync("user-admin"))
                .ReturnsAsync(new UserProfile { AppRole = BowlPoolManager.Core.Constants.Roles.Admin });

            var sut = BuildFunctions(
                new Mock<IHoopsPoolRepository>(),
                new Mock<IHoopsGameRepository>(),
                new Mock<IHoopsEntryRepository>(),
                new Mock<IArchiveRepository>(),
                userRepo);

            var result = await sut.ArchiveHoopsPool(req, "pool-abc");

            result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // ── ArchiveHoopsPool — pool validation guard ──────────────────────────────

        [Fact]
        public async Task ArchiveHoopsPool_ShouldReturn404_WhenPoolDoesNotExist()
        {
            var ctx = new Mock<FunctionContext>().Object;
            var (req, resp) = BuildRequest(ctx, swaHeaderValue: EncodeSuperAdminPrincipal("user-sa"));

            var userRepo = new Mock<IUserRepository>();
            userRepo
                .Setup(r => r.GetUserAsync("user-sa"))
                .ReturnsAsync(new UserProfile { AppRole = BowlPoolManager.Core.Constants.Roles.SuperAdmin });

            var poolRepo = new Mock<IHoopsPoolRepository>();
            poolRepo
                .Setup(r => r.GetPoolAsync("missing-pool"))
                .ReturnsAsync((HoopsPool?)null);

            var sut = BuildFunctions(
                poolRepo,
                new Mock<IHoopsGameRepository>(),
                new Mock<IHoopsEntryRepository>(),
                new Mock<IArchiveRepository>(),
                userRepo);

            var result = await sut.ArchiveHoopsPool(req, "missing-pool");

            result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task ArchiveHoopsPool_ShouldReturn400_WhenPoolIsNotConcluded()
        {
            var ctx = new Mock<FunctionContext>().Object;
            var (req, resp) = BuildRequest(ctx, swaHeaderValue: EncodeSuperAdminPrincipal("user-sa"));

            var userRepo = new Mock<IUserRepository>();
            userRepo
                .Setup(r => r.GetUserAsync("user-sa"))
                .ReturnsAsync(new UserProfile { AppRole = BowlPoolManager.Core.Constants.Roles.SuperAdmin });

            var poolRepo = new Mock<IHoopsPoolRepository>();
            poolRepo
                .Setup(r => r.GetPoolAsync("pool-open"))
                .ReturnsAsync(new HoopsPool
                {
                    Id = "pool-open",
                    IsConcluded = false
                });

            var sut = BuildFunctions(
                poolRepo,
                new Mock<IHoopsGameRepository>(),
                new Mock<IHoopsEntryRepository>(),
                new Mock<IArchiveRepository>(),
                userRepo);

            var result = await sut.ArchiveHoopsPool(req, "pool-open");

            result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // ── GetHoopsArchive ───────────────────────────────────────────────────────

        [Fact]
        public async Task GetHoopsArchive_ShouldReturn404_WhenArchiveDoesNotExist()
        {
            var ctx = new Mock<FunctionContext>().Object;
            var (req, resp) = BuildRequest(ctx, swaHeaderValue: null);

            var archiveRepo = new Mock<IArchiveRepository>();
            archiveRepo
                .Setup(r => r.GetArchiveAsync("HoopsArchive_pool-xyz"))
                .ReturnsAsync((PoolArchive?)null);

            var sut = BuildFunctions(
                new Mock<IHoopsPoolRepository>(),
                new Mock<IHoopsGameRepository>(),
                new Mock<IHoopsEntryRepository>(),
                archiveRepo,
                new Mock<IUserRepository>());

            var result = await sut.GetHoopsArchive(req, "pool-xyz");

            result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetHoopsArchive_ShouldConstructArchiveIdWithHoopsPrefix()
        {
            // Verifies the archive ID format is "HoopsArchive_{poolId}", distinct from
            // the football format "Archive_{poolId}".
            var ctx = new Mock<FunctionContext>().Object;
            var (req, resp) = BuildRequest(ctx, swaHeaderValue: null);
            resp.Body.Should().NotBeNull(); // ensure Body is available for WriteAsJsonAsync

            var poolId = "pool-2026-east";
            var expectedArchiveId = $"HoopsArchive_{poolId}";

            var archiveRepo = new Mock<IArchiveRepository>();
            archiveRepo
                .Setup(r => r.GetArchiveAsync(expectedArchiveId))
                .ReturnsAsync((PoolArchive?)null);

            var sut = BuildFunctions(
                new Mock<IHoopsPoolRepository>(),
                new Mock<IHoopsGameRepository>(),
                new Mock<IHoopsEntryRepository>(),
                archiveRepo,
                new Mock<IUserRepository>());

            await sut.GetHoopsArchive(req, poolId);

            // The archive repo must have been called with exactly "HoopsArchive_{poolId}",
            // not "Archive_{poolId}" (the football prefix) or any other variant.
            archiveRepo.Verify(
                r => r.GetArchiveAsync(expectedArchiveId),
                Times.Once);
        }

        [Fact]
        public async Task GetHoopsArchive_ShouldNotQueryWithFootballPrefix()
        {
            // Regression guard: GetHoopsArchive must never call GetArchiveAsync("Archive_{poolId}").
            var ctx = new Mock<FunctionContext>().Object;
            var (req, _) = BuildRequest(ctx, swaHeaderValue: null);

            var poolId = "pool-99";
            var footballArchiveId = $"Archive_{poolId}";

            var archiveRepo = new Mock<IArchiveRepository>();
            archiveRepo
                .Setup(r => r.GetArchiveAsync(It.IsAny<string>()))
                .ReturnsAsync((PoolArchive?)null);

            var sut = BuildFunctions(
                new Mock<IHoopsPoolRepository>(),
                new Mock<IHoopsGameRepository>(),
                new Mock<IHoopsEntryRepository>(),
                archiveRepo,
                new Mock<IUserRepository>());

            await sut.GetHoopsArchive(req, poolId);

            archiveRepo.Verify(
                r => r.GetArchiveAsync(footballArchiveId),
                Times.Never);
        }
    }
}
