namespace BowlPoolManager.Core.Helpers
{
    /// <summary>What a tap on a Leaderboard row does.</summary>
    public enum LeaderboardTapAction
    {
        /// <summary>The row is not tappable.</summary>
        None,
        /// <summary>Open the entry's picks.</summary>
        OpenPicks,
        /// <summary>Open (or close) the small strip under the row with "Remember", and "See picks" once picks are visible.</summary>
        ShowChoices
    }

    /// <summary>
    /// "Find my entry" on the Leaderboard (Release 2, owner decision 2026-11-27): Remember and Forget only. With nothing
    /// remembered in the pool, a tap on someone else's row offers Remember. Once an entry is remembered, a tap on another
    /// row opens its picks; to remember a different entry, the visitor forgets the current one first. Picks open only
    /// where <see cref="PickVisibility.OnPublicPage"/> allows, so before lock nobody sees anyone else's picks.
    /// </summary>
    public static class LeaderboardTap
    {
        /// <param name="isOwnRow">The row is one of the signed-in player's entries or the entry this browser remembers.</param>
        /// <param name="isSignedInOwnEntry">The row is one of the signed-in player's entries.</param>
        /// <param name="hasRememberedRow">This browser remembers an entry that is on this pool's leaderboard.</param>
        /// <param name="poolLocked">The pool has locked, so picks are public.</param>
        public static LeaderboardTapAction For(bool isOwnRow, bool isSignedInOwnEntry, bool hasRememberedRow, bool poolLocked)
        {
            if (isOwnRow || hasRememberedRow)
            {
                return PickVisibility.OnPublicPage(poolLocked, isSignedInOwnEntry) ? LeaderboardTapAction.OpenPicks : LeaderboardTapAction.None;
            }

            return LeaderboardTapAction.ShowChoices;
        }
    }
}
