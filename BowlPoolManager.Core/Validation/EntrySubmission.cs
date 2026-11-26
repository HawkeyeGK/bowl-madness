using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Core.Validation
{
    /// <summary>
    /// The one server-side step that turns what a browser sent into the entry that gets stored (football and
    /// hoops). The browser may only set the bracket name, the picks and the tiebreaker. Everything else
    /// (owner, season, payment status, admin-entered flag, creation time, document type, audit log) comes from
    /// the stored entry or from the server. Rules: BUSINESS-RULES section 3; issues #99, #131 and #134.
    /// </summary>
    public static class EntrySubmission
    {
        public const int MaxNameLength = 60;
        public const int MaxPickLength = 100;
        public const int MaxTieBreakerPoints = 999;

        /// <summary>
        /// Checks the fields a browser is allowed to send. Picks must be for games in the pool. For football, pass
        /// <paramref name="seasonGames"/> (every game of the season): each pick must then be one of the two teams
        /// the entry's own earlier picks lead to (issue 128). A pick the stored entry already carries with the same
        /// value is left alone, so an old entry with a stale pick can still be saved (renamed, for example).
        /// Hoops passes no games and gets only the pool check.
        /// </summary>
        public static ValidationResult Validate(BracketEntry submitted, BracketEntry? existing, IEnumerable<string> poolGameIds,
            IReadOnlyList<BowlGame>? seasonGames = null)
        {
            var name = (submitted.PlayerName ?? string.Empty).Trim();
            if (name.Length == 0) return ValidationResult.Fail("Bracket Name is required.");
            if (name.Length > MaxNameLength) return ValidationResult.Fail($"Bracket Name must be {MaxNameLength} characters or fewer.");

            if (submitted.TieBreakerPoints < 0 || submitted.TieBreakerPoints > MaxTieBreakerPoints)
                return ValidationResult.Fail($"Tiebreaker must be between 0 and {MaxTieBreakerPoints}.");

            var allowed = new HashSet<string>(poolGameIds, StringComparer.OrdinalIgnoreCase);
            var stored = existing?.Picks ?? new Dictionary<string, string>();
            var matchups = seasonGames == null ? null : EntryPickRules.Resolve(seasonGames, submitted.Picks);

            foreach (var pick in submitted.Picks ?? new Dictionary<string, string>())
            {
                if ((pick.Value?.Length ?? 0) > MaxPickLength)
                    return ValidationResult.Fail("A pick is too long.");

                // Unchanged from what is stored: left alone, whatever its state.
                bool unchanged = stored.TryGetValue(pick.Key, out var storedValue)
                                 && string.Equals(storedValue, pick.Value, StringComparison.OrdinalIgnoreCase);
                if (unchanged) continue;

                if (!allowed.Contains(pick.Key))
                    return ValidationResult.Fail("Picks include a game that is not in this pool.");

                if (matchups == null || string.IsNullOrWhiteSpace(pick.Value)) continue;

                var game = seasonGames!.FirstOrDefault(g => string.Equals(g.Id, pick.Key, StringComparison.OrdinalIgnoreCase));
                if (game == null || !matchups.TryGetValue(game.Id, out var matchup) || !EntryPickRules.IsValidPick(matchup, pick.Value))
                    return ValidationResult.Fail($"The pick for {game?.BowlName ?? "a game"} is not a team that can play in that game. " +
                                                 "Pick the earlier games first, then pick again.");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Every entry an Admin keys for someone names its owner: an account, or the person's name when they have no
        /// account (Release 2 group C, owner decision 2026-11-25). A player's own save, and an Admin saving with
        /// assignToMe, belong to the caller and need nothing here.
        /// </summary>
        public static ValidationResult ValidateOwner(BracketEntry submitted, bool isAdmin, bool assignToMe)
        {
            if (!isAdmin || assignToMe) return ValidationResult.Success();

            var ownerName = (submitted.OwnerName ?? string.Empty).Trim();
            if (ownerName.Length > MaxNameLength)
                return ValidationResult.Fail($"The owner's name must be {MaxNameLength} characters or fewer.");

            if (string.IsNullOrWhiteSpace(submitted.UserId) && ownerName.Length == 0)
                return ValidationResult.Fail("Say who this entry belongs to: choose their account, or type their name.");

            return ValidationResult.Success();
        }

        /// <summary>
        /// Builds the entry to store. The audit log is left for the caller to fill in.
        /// An Admin keeps the owner they chose (an Admin may enter or reassign an entry for someone), unless
        /// assignToMe is set; a player's save is always recorded as theirs. IsAdminEntered is set on a new entry
        /// that an Admin creates for another user (issue 161); an existing entry keeps its stored value.
        /// An Admin who types an owner's name without choosing an account leaves the entry with no account
        /// (<see cref="BracketEntry.OwnerName"/> holds the name); choosing an account drops the typed name.
        /// </summary>
        public static BracketEntry Build(BracketEntry submitted, BracketEntry? existing, string poolId, string seasonId,
            string documentType, string callerUserId, bool isAdmin, bool assignToMe)
        {
            string userId;
            string? ownerName;
            // Only football entries take a typed owner for now; hoops keeps its Phase 1.1 owner rules.
            var typedOwner = documentType == Constants.DocumentTypes.BracketEntry ? (submitted.OwnerName ?? string.Empty).Trim() : string.Empty;
            if (!isAdmin) { userId = callerUserId; ownerName = existing?.OwnerName; }
            else if (assignToMe) { userId = callerUserId; ownerName = null; }
            else if (!string.IsNullOrWhiteSpace(submitted.UserId)) { userId = submitted.UserId.Trim(); ownerName = null; }
            else if (typedOwner.Length > 0) { userId = string.Empty; ownerName = typedOwner; }
            else { userId = existing?.UserId ?? callerUserId; ownerName = existing?.OwnerName; }

            return new BracketEntry
            {
                // An existing entry keeps its id; a new one keeps the browser's id only when it is a GUID (what the pages generate).
                Id = existing != null ? existing.Id : Guid.TryParse(submitted.Id, out _) ? submitted.Id : Guid.NewGuid().ToString(),
                SeasonId = seasonId,
                PoolId = poolId,
                UserId = userId,
                OwnerName = ownerName,
                PlayerName = (submitted.PlayerName ?? string.Empty).Trim(),
                Picks = new Dictionary<string, string>(submitted.Picks ?? new Dictionary<string, string>()),
                TieBreakerPoints = submitted.TieBreakerPoints,
                CreatedOn = existing?.CreatedOn ?? DateTime.UtcNow,
                IsPaid = existing?.IsPaid ?? false,
                IsAdminEntered = existing?.IsAdminEntered ?? (isAdmin && !string.Equals(userId, callerUserId, StringComparison.Ordinal)),
                Type = documentType,
                AuditLog = existing?.AuditLog != null ? new List<string>(existing.AuditLog) : new List<string>()
            };
        }
    }
}
