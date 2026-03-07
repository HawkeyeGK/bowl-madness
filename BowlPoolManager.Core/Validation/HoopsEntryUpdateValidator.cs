using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Core.Validation
{
    public static class HoopsEntryUpdateValidator
    {
        public static ValidationResult ValidateUpdate(HoopsPool pool, BracketEntry? existingEntry, BracketEntry newEntry, bool isAdmin)
        {
            // 1. Admin Bypass
            if (isAdmin)
            {
                return ValidationResult.Success();
            }

            // 2. Check Lock Deadline — if before lock date, everything is allowed
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

            // 5. Immutability Check — picks cannot change after lock; name edits are allowed
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

            if (picksChanged)
            {
                return ValidationResult.Fail("Cannot change picks after pool is locked.");
            }

            return ValidationResult.Success();
        }
    }
}
