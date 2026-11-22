using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BowlPoolManager.Api.Functions;
using BowlPoolManager.Api.Helpers;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// BUSINESS-RULES section 1: a disabled user is treated exactly like an unauthenticated user, on
    /// every endpoint. Today the server never reads IsDisabled (#98). One test goes through the shared
    /// SuperAdmin check and one through an entry endpoint that reads the sign-in header directly.
    /// </summary>
    public class EndpointAuthDisabledAccountTests
    {
        // Known failure: #98. A disabled SuperAdmin must be refused like an anonymous caller (401).
        [Fact]
        [Trait("Category", "KnownFailure")]
        public async Task ValidateSuperAdminAsync_ShouldReturn401_WhenSuperAdminIsDisabled()
        {
            var req = EndpointKit.Request(EndpointKit.DisabledSuperAdminId);

            var result = await SecurityHelper.ValidateSuperAdminAsync(req, EndpointKit.Users().Object);

            result.IsValid.Should().BeFalse("a disabled account is treated as not signed in");
            result.ErrorResponse.Should().NotBeNull();
            EndpointKit.Status(result.ErrorResponse!).Should().Be(401);
        }

        // Known failure: #98. A disabled player must not delete their own entry.
        [Fact]
        [Trait("Category", "KnownFailure")]
        public async Task DeleteEntry_ShouldReturn401AndDeleteNothing_WhenCallerIsDisabled()
        {
            var entries = new Mock<IEntryRepository>();
            entries.Setup(e => e.GetEntryAsync("e1", "season-1")).ReturnsAsync(new BracketEntry
            {
                Id = "e1", SeasonId = "season-1", PoolId = "pool-1", UserId = EndpointKit.DisabledPlayerId
            });
            var pools = new Mock<IPoolRepository>();
            pools.Setup(p => p.GetPoolAsync(It.IsAny<string>())).ReturnsAsync(new BowlPool
            {
                Id = "pool-1", SeasonId = "season-1", LockDate = DateTime.UtcNow.AddDays(7)
            });
            var sut = new EntryFunctions(new NullLoggerFactory(), entries.Object, pools.Object,
                new Mock<IGameRepository>().Object, EndpointKit.Users().Object);

            var result = await sut.DeleteEntry(EndpointKit.Request(EndpointKit.DisabledPlayerId, "?seasonId=season-1"), "e1");

            EndpointKit.Status(result).Should().Be(401);
            entries.Verify(e => e.DeleteEntryAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        // Guard for the fix: an enabled account must keep working through the same shared check.
        [Fact]
        public async Task ValidateSuperAdminAsync_ShouldBeValid_WhenSuperAdminIsEnabled()
        {
            var req = EndpointKit.Request(EndpointKit.SuperAdminId);

            var result = await SecurityHelper.ValidateSuperAdminAsync(req, EndpointKit.Users().Object);

            result.IsValid.Should().BeTrue();
        }
    }
}
