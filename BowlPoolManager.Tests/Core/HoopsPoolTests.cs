using FluentAssertions;
using BowlPoolManager.Core;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Core
{
    public class HoopsPoolTests
    {
        [Fact]
        public void HoopsPool_ShouldInitializeWithDefaults()
        {
            var pool = new HoopsPool();

            pool.Id.Should().NotBeNullOrEmpty();
            pool.SeasonId.Should().BeEmpty();
            pool.Name.Should().BeEmpty();
            pool.Season.Should().Be(DateTime.UtcNow.Year);
            pool.GameIds.Should().NotBeNull().And.BeEmpty();
            pool.InviteCode.Should().BeEmpty();
            pool.IsConcluded.Should().BeFalse();
            pool.IsArchived.Should().BeFalse();
            pool.PointsPerRound.Should().BeNull();
        }

        [Fact]
        public void HoopsPool_ShouldGenerateUniqueIds()
        {
            var pool1 = new HoopsPool();
            var pool2 = new HoopsPool();

            pool1.Id.Should().NotBe(pool2.Id);
        }

        [Fact]
        public void HoopsPool_Type_ShouldMatchHoopsPoolDocumentTypeConstant()
        {
            var pool = new HoopsPool();

            pool.Type.Should().Be(Constants.DocumentTypes.HoopsPool);
        }

        [Fact]
        public void HoopsPool_Type_ShouldBeLiterallyHoopsPool()
        {
            // Guards against accidental rename of the constant value without a migration.
            var pool = new HoopsPool();

            pool.Type.Should().Be("HoopsPool");
        }

        [Fact]
        public void HoopsPool_LockDate_ShouldDefaultToFuture()
        {
            var before = DateTime.UtcNow;

            var pool = new HoopsPool();

            pool.LockDate.Should().BeAfter(before);
        }

        [Fact]
        public void HoopsPool_GameIds_ShouldSupportAddingGames()
        {
            var pool = new HoopsPool();

            pool.GameIds.Add("game-1");
            pool.GameIds.Add("game-2");

            pool.GameIds.Should().HaveCount(2)
                .And.Contain("game-1")
                .And.Contain("game-2");
        }

        [Fact]
        public void HoopsPool_ArchiveFlags_ShouldWorkIndependently()
        {
            var pool = new HoopsPool();

            pool.IsConcluded = true;

            pool.IsConcluded.Should().BeTrue();
            pool.IsArchived.Should().BeFalse();

            pool.IsArchived = true;

            pool.IsConcluded.Should().BeTrue();
            pool.IsArchived.Should().BeTrue();
        }

        [Fact]
        public void HoopsPool_PointsPerRound_ShouldAcceptRoundMappings()
        {
            var pool = new HoopsPool();

            pool.PointsPerRound = new Dictionary<TournamentRound, int>
            {
                { TournamentRound.RoundOf64,          1 },
                { TournamentRound.RoundOf32,          2 },
                { TournamentRound.Sweet16,            4 },
                { TournamentRound.Elite8,             8 },
                { TournamentRound.FinalFour,         16 },
                { TournamentRound.NationalChampionship, 32 }
            };

            pool.PointsPerRound.Should().HaveCount(6);
            pool.PointsPerRound[TournamentRound.NationalChampionship].Should().Be(32);
            pool.PointsPerRound[TournamentRound.RoundOf64].Should().Be(1);
        }

        [Fact]
        public void HoopsPool_Properties_ShouldBeSettable()
        {
            var pool = new HoopsPool
            {
                Id = "pool-abc",
                SeasonId = "season-2026",
                Name = "March Madness 2026",
                Season = 2026,
                InviteCode = "DUKE26"
            };

            pool.Id.Should().Be("pool-abc");
            pool.SeasonId.Should().Be("season-2026");
            pool.Name.Should().Be("March Madness 2026");
            pool.Season.Should().Be(2026);
            pool.InviteCode.Should().Be("DUKE26");
        }
    }
}
