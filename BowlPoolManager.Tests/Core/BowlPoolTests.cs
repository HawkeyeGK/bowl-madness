using Xunit;
using FluentAssertions;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Core
{
    public class BowlPoolTests
    {
        [Fact]
        public void BowlPool_ShouldInitializeWithDefaults()
        {
            // Act
            var pool = new BowlPool();

            // Assert
            pool.Id.Should().NotBeNullOrEmpty();
            pool.SeasonId.Should().BeEmpty();
            pool.Name.Should().BeEmpty();
            pool.Season.Should().Be(DateTime.Now.Year);
            pool.GameIds.Should().NotBeNull();
            pool.GameIds.Should().BeEmpty();
            pool.InviteCode.Should().BeEmpty();
            pool.IsConcluded.Should().BeFalse();
            pool.IsArchived.Should().BeFalse();
        }

        [Fact]
        public void BowlPool_ShouldGenerateUniqueIds()
        {
            // Act
            var pool1 = new BowlPool();
            var pool2 = new BowlPool();

            // Assert
            pool1.Id.Should().NotBe(pool2.Id);
        }

        [Fact]
        public void BowlPool_TieBreakerConfig_ShouldDefaultToCorrectPicksFirst()
        {
            // Act
            var pool = new BowlPool();

            // Assert
            pool.PrimaryTieBreaker.Should().Be(TieBreakerMetric.CorrectPickCount);
            pool.SecondaryTieBreaker.Should().Be(TieBreakerMetric.ScoreDelta);
        }

        [Fact]
        public void BowlPool_TieBreakerConfig_ShouldAllowSwapping()
        {
            // Arrange
            var pool = new BowlPool();

            // Act
            pool.PrimaryTieBreaker = TieBreakerMetric.ScoreDelta;
            pool.SecondaryTieBreaker = TieBreakerMetric.CorrectPickCount;

            // Assert
            pool.PrimaryTieBreaker.Should().Be(TieBreakerMetric.ScoreDelta);
            pool.SecondaryTieBreaker.Should().Be(TieBreakerMetric.CorrectPickCount);
        }

        [Fact]
        public void BowlPool_LockDate_ShouldDefaultToFuture()
        {
            // Arrange
            var before = DateTime.UtcNow;

            // Act
            var pool = new BowlPool();

            // Assert
            pool.LockDate.Should().BeAfter(before);
        }

        [Fact]
        public void BowlPool_GameIds_ShouldSupportAddingGames()
        {
            // Arrange
            var pool = new BowlPool();

            // Act
            pool.GameIds.Add("game-1");
            pool.GameIds.Add("game-2");
            pool.GameIds.Add("game-3");

            // Assert
            pool.GameIds.Should().HaveCount(3);
            pool.GameIds.Should().Contain("game-1");
            pool.GameIds.Should().Contain("game-2");
            pool.GameIds.Should().Contain("game-3");
        }

        [Fact]
        public void BowlPool_Type_ShouldBeSetCorrectly()
        {
            // Act
            var pool = new BowlPool();

            // Assert
            pool.Type.Should().Be("BowlPool");
        }

        [Fact]
        public void BowlPool_ArchiveFlags_ShouldWorkIndependently()
        {
            // Arrange
            var pool = new BowlPool();

            // Act - Conclude but don't archive yet
            pool.IsConcluded = true;

            // Assert
            pool.IsConcluded.Should().BeTrue();
            pool.IsArchived.Should().BeFalse();

            // Act - Now archive
            pool.IsArchived = true;

            // Assert
            pool.IsConcluded.Should().BeTrue();
            pool.IsArchived.Should().BeTrue();
        }
    }
}
