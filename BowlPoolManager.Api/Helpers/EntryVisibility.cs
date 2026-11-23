using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Api.Helpers
{
    /// <summary>
    /// Who may see what on an entry that is read through the entry endpoints (football and hoops).
    /// BUSINESS-RULES section 3: before lock only the owner and Admins see picks; after lock everyone does.
    /// The owner id and the audit log (which holds editors' email addresses) are never shown to anyone but the
    /// owner and Admins. Issue #97.
    /// </summary>
    public static class EntryVisibility
    {
        public static bool IsLocked(DateTime lockDateUtc) => DateTime.UtcNow > lockDateUtc;

        public static bool IsOwner(BracketEntry entry, SecurityHelper.Caller? caller) =>
            caller != null && !string.IsNullOrEmpty(entry.UserId) && entry.UserId == caller.UserId;

        /// <summary>
        /// The entry as this caller may see it. Admins and the owner get the stored entry. Anyone else gets a copy with
        /// no owner id and no audit log, and before lock also no picks (null) and no tiebreaker.
        /// </summary>
        public static BracketEntry ForViewer(BracketEntry entry, bool poolLocked, SecurityHelper.Caller? caller)
        {
            if (caller != null && (caller.IsAdmin || IsOwner(entry, caller))) return entry;

            return new BracketEntry
            {
                Id = entry.Id,
                SeasonId = entry.SeasonId,
                PoolId = entry.PoolId,
                UserId = string.Empty,
                PlayerName = entry.PlayerName,
                Picks = poolLocked ? entry.Picks : null,
                TieBreakerPoints = poolLocked ? entry.TieBreakerPoints : 0,
                CreatedOn = entry.CreatedOn,
                IsPaid = entry.IsPaid,
                IsAdminEntered = entry.IsAdminEntered,
                Type = entry.Type,
                AuditLog = new List<string>()
            };
        }
    }
}
