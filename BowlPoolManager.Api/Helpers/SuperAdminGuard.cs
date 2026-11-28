using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Helpers
{
    /// <summary>
    /// Owner rule (2026-11-28, issue 138): the site always keeps at least one active SuperAdmin. A role change or a
    /// disable that would leave none is refused, including a SuperAdmin demoting or disabling their own account.
    /// </summary>
    public static class SuperAdminGuard
    {
        public const string Refusal =
            "This would leave the site with no active SuperAdmin. Make another account SuperAdmin first.";

        private static bool IsActiveSuperAdmin(UserProfile user) =>
            !user.IsDisabled && user.AppRole == Constants.Roles.SuperAdmin;

        /// <summary>
        /// True when the change removes an active SuperAdmin (demotes or disables one). Only then can the count reach
        /// zero, so callers list all users only when this is true.
        /// </summary>
        public static bool RemovesAnActiveSuperAdmin(UserProfile target, string newRole, bool newDisabled) =>
            IsActiveSuperAdmin(target) && (newRole != Constants.Roles.SuperAdmin || newDisabled);

        /// <summary>
        /// True when changing <paramref name="target"/> to <paramref name="newRole"/> and <paramref name="newDisabled"/>
        /// would leave no active SuperAdmin among <paramref name="allUsers"/>.
        /// </summary>
        public static bool LeavesNoActiveSuperAdmin(IEnumerable<UserProfile> allUsers, UserProfile target, string newRole, bool newDisabled)
        {
            if (!RemovesAnActiveSuperAdmin(target, newRole, newDisabled)) return false;

            return !allUsers.Any(u => !string.Equals(u.Id, target.Id, StringComparison.Ordinal) && IsActiveSuperAdmin(u));
        }
    }
}
