using Xunit;
using FluentAssertions;
using BowlPoolManager.Core.Validation;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core.Validation
{
    public class EntryUpdateValidatorTests
    {
        private BowlPool CreatePool(bool isLocked, bool isConcluded)
        {
            return new BowlPool
            {
                Id = "pool-1",
                LockDate = isLocked ? DateTime.UtcNow.AddDays(-1) : DateTime.UtcNow.AddDays(1),
                IsConcluded = isConcluded,
                IsArchived = isConcluded // Usually synonymous
            };
        }

        private BracketEntry CreateEntry(string name = "My Entry")
        {
            return new BracketEntry
            {
                Id = "entry-1",
                PlayerName = name,
                TieBreakerPoints = 50,
                Picks = new Dictionary<string, string> { { "game-1", "TeamA" } }
            };
        }

        [Fact]
        public void ValidateUpdate_PoolUnlocked_AllowsEverything()
        {
            var pool = CreatePool(isLocked: false, isConcluded: false);
            var existing = CreateEntry();
            var updated = CreateEntry();
            updated.TieBreakerPoints = 99; // Changed
            updated.Picks["game-1"] = "TeamB"; // Changed

            var result = EntryUpdateValidator.ValidateUpdate(pool, existing, updated, isAdmin: false);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void ValidateUpdate_PoolLocked_AllowsNameChange()
        {
            var pool = CreatePool(isLocked: true, isConcluded: false);
            var existing = CreateEntry("Old Name");
            var updated = CreateEntry("New Name"); // Changed Name ONLY

            var result = EntryUpdateValidator.ValidateUpdate(pool, existing, updated, isAdmin: false);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void ValidateUpdate_PoolLocked_RejectsTiebreakerChange()
        {
            var pool = CreatePool(isLocked: true, isConcluded: false);
            var existing = CreateEntry();
            var updated = CreateEntry();
            updated.TieBreakerPoints = 100; // Changed

            var result = EntryUpdateValidator.ValidateUpdate(pool, existing, updated, isAdmin: false);

            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Tiebreaker");
        }

        [Fact]
        public void ValidateUpdate_PoolLocked_RejectsPickChange()
        {
            var pool = CreatePool(isLocked: true, isConcluded: false);
            var existing = CreateEntry();
            var updated = CreateEntry();
            updated.Picks["game-1"] = "TeamB"; // Changed

            var result = EntryUpdateValidator.ValidateUpdate(pool, existing, updated, isAdmin: false);

            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Picks");
        }

        [Fact]
        public void ValidateUpdate_PoolLocked_RejectsPickCountChange()
        {
            var pool = CreatePool(isLocked: true, isConcluded: false);
            var existing = CreateEntry();
            var updated = CreateEntry();
            updated.Picks.Add("game-2", "New Pick"); // Added

            var result = EntryUpdateValidator.ValidateUpdate(pool, existing, updated, isAdmin: false);

            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Picks");
        }

        [Fact]
        public void ValidateUpdate_PoolConcluded_RejectsNameChange()
        {
            var pool = CreatePool(isLocked: true, isConcluded: true);
            var existing = CreateEntry("Old Name");
            var updated = CreateEntry("New Name");

            var result = EntryUpdateValidator.ValidateUpdate(pool, existing, updated, isAdmin: false);

            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Contain("concluded");
        }

        [Fact]
        public void ValidateUpdate_Admin_BypassesLocks()
        {
            var pool = CreatePool(isLocked: true, isConcluded: true); // Concluded!
            var existing = CreateEntry();
            var updated = CreateEntry();
            updated.TieBreakerPoints = 1000; // Changed

            var result = EntryUpdateValidator.ValidateUpdate(pool, existing, updated, isAdmin: true);

            result.IsValid.Should().BeTrue();
        }
        
        [Fact]
        public void ValidateUpdate_PoolLocked_RejectsNewEntry()
        {
            var pool = CreatePool(isLocked: true, isConcluded: false);
            BracketEntry? existing = null; // New Entry
            var updated = CreateEntry();

            var result = EntryUpdateValidator.ValidateUpdate(pool, existing, updated, isAdmin: false);

            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Contain("new entries");
        }
    }
}
