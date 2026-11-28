using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BowlPoolManager.Api.Functions;
using BowlPoolManager.Api.Helpers;
using BowlPoolManager.Api.Repositories;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// Owner rule (2026-11-28, issue 138): the site always keeps at least one active SuperAdmin. UpdateUserRole and
    /// ToggleUserStatus refuse a change that would leave none, including a SuperAdmin demoting or disabling themselves.
    /// </summary>
    public class LastSuperAdminTests
    {
        private const string Me = "super-me";
        private const string Other = "super-other";

        private static UserProfile Super(string id, bool disabled = false) =>
            new() { Id = id, Email = id + "@test.com", AppRole = Constants.Roles.SuperAdmin, IsDisabled = disabled };

        private static (UserFunctions sut, Mock<IUserRepository> repo) Build(params UserProfile[] profiles)
        {
            var byId = profiles.ToDictionary(p => p.Id);
            var repo = new Mock<IUserRepository>();
            repo.Setup(r => r.GetUserAsync(It.IsAny<string>())).ReturnsAsync((string id) => byId.TryGetValue(id, out var p) ? p : null);
            repo.Setup(r => r.GetUsersAsync()).ReturnsAsync(() => byId.Values.ToList());
            repo.Setup(r => r.UpsertUserAsync(It.IsAny<UserProfile>())).Returns(Task.CompletedTask);
            return (new UserFunctions(new NullLoggerFactory(), repo.Object, new ConfigurationBuilder().Build()), repo);
        }

        // ── SuperAdminGuard ──────────────────────────────────────────────────────

        [Theory]
        [InlineData(Constants.Roles.Admin, false)]
        [InlineData(Constants.Roles.Player, false)]
        [InlineData(Constants.Roles.SuperAdmin, true)]
        public void LeavesNoActiveSuperAdmin_ShouldBeTrue_WhenTheOnlyActiveSuperAdminIsDemotedOrDisabled(string newRole, bool newDisabled)
        {
            var me = Super(Me);
            var users = new[] { me, Super(Other, disabled: true) };

            SuperAdminGuard.LeavesNoActiveSuperAdmin(users, me, newRole, newDisabled).Should().BeTrue();
        }

        [Fact]
        public void LeavesNoActiveSuperAdmin_ShouldBeFalse_WhenAnotherActiveSuperAdminRemains()
        {
            var me = Super(Me);
            var users = new[] { me, Super(Other) };

            SuperAdminGuard.LeavesNoActiveSuperAdmin(users, me, Constants.Roles.Player, false).Should().BeFalse();
        }

        [Fact]
        public void LeavesNoActiveSuperAdmin_ShouldBeFalse_WhenTheTargetIsNotAnActiveSuperAdmin()
        {
            var admin = new UserProfile { Id = "a", AppRole = Constants.Roles.Admin };
            var disabledSuper = Super("d", disabled: true);

            SuperAdminGuard.LeavesNoActiveSuperAdmin(new[] { admin }, admin, Constants.Roles.Player, true).Should().BeFalse();
            SuperAdminGuard.LeavesNoActiveSuperAdmin(new[] { disabledSuper }, disabledSuper, Constants.Roles.Player, true).Should().BeFalse();
        }

        [Fact]
        public void LeavesNoActiveSuperAdmin_ShouldBeFalse_WhenTheSuperAdminStaysActive()
        {
            var me = Super(Me);

            SuperAdminGuard.LeavesNoActiveSuperAdmin(new[] { me }, me, Constants.Roles.SuperAdmin, false).Should().BeFalse();
        }

        // ── UpdateUserRole ───────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateUserRole_ShouldRefuseAndSaveNothing_WhenTheLastActiveSuperAdminDemotesThemselves()
        {
            var (sut, repo) = Build(Super(Me), Super(Other, disabled: true));

            var result = await sut.UpdateUserRole(EndpointKit.Request(Me, $"?userId={Me}&role={Constants.Roles.Admin}"));

            EndpointKit.Status(result).Should().Be(400);
            EndpointKit.BodyText(result).Should().Contain("no active SuperAdmin");
            repo.Verify(r => r.UpsertUserAsync(It.IsAny<UserProfile>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUserRole_ShouldSave_WhenAnotherActiveSuperAdminRemains()
        {
            var (sut, repo) = Build(Super(Me), Super(Other));

            var result = await sut.UpdateUserRole(EndpointKit.Request(Me, $"?userId={Other}&role={Constants.Roles.Player}"));

            EndpointKit.Status(result).Should().Be(200);
            repo.Verify(r => r.UpsertUserAsync(It.Is<UserProfile>(u => u.Id == Other && u.AppRole == Constants.Roles.Player)), Times.Once);
        }

        [Fact]
        public async Task UpdateUserRole_ShouldRefuseBeforeTheRuleIsChecked_WhenCallerIsAdmin()
        {
            var admin = new UserProfile { Id = "admin", AppRole = Constants.Roles.Admin };
            var (sut, repo) = Build(Super(Me), admin);

            var result = await sut.UpdateUserRole(EndpointKit.Request("admin", $"?userId={Me}&role={Constants.Roles.Player}"));

            EndpointKit.Status(result).Should().Be(403);
            repo.Verify(r => r.GetUsersAsync(), Times.Never);
            repo.Verify(r => r.UpsertUserAsync(It.IsAny<UserProfile>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUserRole_ShouldNotListUsers_WhenTheTargetIsNotASuperAdmin()
        {
            var player = new UserProfile { Id = "p", AppRole = Constants.Roles.Player };
            var (sut, repo) = Build(Super(Me), player);

            var result = await sut.UpdateUserRole(EndpointKit.Request(Me, $"?userId=p&role={Constants.Roles.Admin}"));

            EndpointKit.Status(result).Should().Be(200);
            repo.Verify(r => r.GetUsersAsync(), Times.Never);
        }

        // ── ToggleUserStatus ─────────────────────────────────────────────────────

        [Fact]
        public async Task ToggleUserStatus_ShouldRefuseAndSaveNothing_WhenTheLastActiveSuperAdminDisablesThemselves()
        {
            var me = Super(Me);
            var (sut, repo) = Build(me);

            var result = await sut.ToggleUserStatus(EndpointKit.Request(Me, $"?userId={Me}"));

            EndpointKit.Status(result).Should().Be(400);
            EndpointKit.BodyText(result).Should().Contain("no active SuperAdmin");
            me.IsDisabled.Should().BeFalse();
            repo.Verify(r => r.UpsertUserAsync(It.IsAny<UserProfile>()), Times.Never);
        }

        [Fact]
        public async Task ToggleUserStatus_ShouldDisableAnotherSuperAdmin_WhenTheCallerStaysActive()
        {
            var (sut, repo) = Build(Super(Me), Super(Other));

            var result = await sut.ToggleUserStatus(EndpointKit.Request(Me, $"?userId={Other}"));

            EndpointKit.Status(result).Should().Be(200);
            repo.Verify(r => r.UpsertUserAsync(It.Is<UserProfile>(u => u.Id == Other && u.IsDisabled)), Times.Once);
        }

        [Fact]
        public async Task ToggleUserStatus_ShouldReEnableASuperAdmin_WhenTheyAreDisabled()
        {
            var (sut, repo) = Build(Super(Me), Super(Other, disabled: true));

            var result = await sut.ToggleUserStatus(EndpointKit.Request(Me, $"?userId={Other}"));

            EndpointKit.Status(result).Should().Be(200);
            repo.Verify(r => r.UpsertUserAsync(It.Is<UserProfile>(u => u.Id == Other && !u.IsDisabled)), Times.Once);
        }
    }
}
