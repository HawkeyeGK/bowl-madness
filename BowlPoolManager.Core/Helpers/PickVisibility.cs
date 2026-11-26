namespace BowlPoolManager.Core.Helpers
{
    /// <summary>
    /// Whether a public page (Scores, Entry details) draws an entry's picks. Before lock only the signed-in owner's own
    /// entry; an Admin sees other players' picks before lock only in the admin section (owner decision 2026-11-25).
    /// The API also hides picks from everyone but the owner and Admins before lock (BUSINESS-RULES section 3).
    /// </summary>
    public static class PickVisibility
    {
        public static bool OnPublicPage(bool poolLocked, bool isViewersOwnEntry) => poolLocked || isViewersOwnEntry;
    }
}
