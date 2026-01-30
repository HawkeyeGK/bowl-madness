using Xunit;
using FluentAssertions;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Core
{
    public class BracketEntryTests
    {
        [Fact]
        public void BracketEntry_ShouldInitializeWithDefaults()
        {
            // Act
            var entry = new BracketEntry();

            // Assert
            entry.Id.Should().NotBeNullOrEmpty();
            entry.SeasonId.Should().BeEmpty();
            entry.PoolId.Should().BeEmpty();
            entry.UserId.Should().BeEmpty();
            entry.PlayerName.Should().BeEmpty();
            entry.Picks.Should().NotBeNull();
            entry.Picks.Should().BeEmpty();
            entry.TieBreakerPoints.Should().Be(0);
            entry.IsPaid.Should().BeFalse();
            entry.IsAdminEntered.Should().BeFalse();
            entry.AuditLog.Should().NotBeNull();
            entry.AuditLog.Should().BeEmpty();
        }

        [Fact]
        public void BracketEntry_ShouldGenerateUniqueIds()
        {
            // Act
            var entry1 = new BracketEntry();
            var entry2 = new BracketEntry();

            // Assert
            entry1.Id.Should().NotBe(entry2.Id);
        }

        [Fact]
        public void BracketEntry_PicksDictionary_ShouldSupportNullForRedaction()
        {
            // Arrange
            var entry = new BracketEntry();
            
            // Act - Simulate redaction
            entry.Picks = null;

            // Assert
            entry.Picks.Should().BeNull();
        }

        [Fact]
        public void BracketEntry_PicksDictionary_ShouldStorePicks()
        {
            // Arrange
            var entry = new BracketEntry();
            
            // Act
            entry.Picks = new Dictionary<string, string>
            {
                { "game1", "Team A" },
                { "game2", "Team B" },
                { "game3", "Team C" }
            };

            // Assert
            entry.Picks.Should().HaveCount(3);
            entry.Picks["game1"].Should().Be("Team A");
            entry.Picks["game2"].Should().Be("Team B");
            entry.Picks["game3"].Should().Be("Team C");
        }

        [Fact]
        public void BracketEntry_Type_ShouldBeSetCorrectly()
        {
            // Act
            var entry = new BracketEntry();

            // Assert
            entry.Type.Should().Be("BracketEntry");
        }

        [Fact]
        public void BracketEntry_CreatedOn_ShouldBeRecentUtc()
        {
            // Arrange
            var before = DateTime.UtcNow.AddSeconds(-1);
            
            // Act
            var entry = new BracketEntry();
            var after = DateTime.UtcNow.AddSeconds(1);

            // Assert
            entry.CreatedOn.Should().BeOnOrAfter(before);
            entry.CreatedOn.Should().BeOnOrBefore(after);
        }

        [Fact]
        public void BracketEntry_AuditLog_ShouldAcceptEntries()
        {
            // Arrange
            var entry = new BracketEntry();
            
            // Act
            entry.AuditLog.Add("2025-01-15: Entry created");
            entry.AuditLog.Add("2025-01-16: Picks updated");

            // Assert
            entry.AuditLog.Should().HaveCount(2);
            entry.AuditLog[0].Should().Contain("Entry created");
        }
    }
}
