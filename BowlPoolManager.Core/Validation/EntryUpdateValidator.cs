using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Core.Validation
{
    public static class EntryUpdateValidator
    {
        public static ValidationResult ValidateUpdate(BowlPool pool, BracketEntry? existingEntry, BracketEntry newEntry, bool isAdmin)
        {
            // 1. Admin Bypass
            if (isAdmin)
            {
                return ValidationResult.Success();
            }

            // 2. Check Lock Deadline
            // If we are BEFORE the lock date, everything is allowed.
            if (DateTime.UtcNow <= pool.LockDate)
            {
                return ValidationResult.Success();
            }

            // --- POOL IS LOCKED ---

            // 3. Concluded/Archived Check
            if (pool.IsConcluded || pool.IsArchived)
            {
                return ValidationResult.Fail("This pool is concluded. No changes allowed.");
            }

            // 4. New Entry Check (Cannot join after lock)
            if (existingEntry == null)
            {
                return ValidationResult.Fail("This pool is locked. No new entries allowed.");
            }

            // 5. Immutability Check (Picks and Tiebreaker)
            // Allow Name Edits, but reject Picks/Tiebreaker changes
            
            bool tiebreakerChanged = existingEntry.TieBreakerPoints != newEntry.TieBreakerPoints;
            
            var oldPicks = existingEntry.Picks ?? new Dictionary<string, string>();
            var newPicks = newEntry.Picks ?? new Dictionary<string, string>();
            
            bool picksChanged = oldPicks.Count != newPicks.Count;
            if (!picksChanged)
            {
                foreach (var kv in oldPicks)
                {
                    if (!newPicks.TryGetValue(kv.Key, out var newVal) || 
                        !string.Equals(newVal, kv.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        picksChanged = true;
                        break;
                    }
                }
            }

            if (tiebreakerChanged || picksChanged)
            {
                return ValidationResult.Fail("Cannot change Picks or Tiebreaker after pool is locked.");
            }

            return ValidationResult.Success();
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; private set; }
        public string ErrorMessage { get; private set; } = string.Empty;

        public static ValidationResult Success() => new ValidationResult { IsValid = true };
        public static ValidationResult Fail(string message) => new ValidationResult { IsValid = false, ErrorMessage = message };
    }
}
