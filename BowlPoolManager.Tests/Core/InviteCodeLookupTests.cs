using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>Issue 135: an invite code that matches more than one pool opens the current season's pool.</summary>
    public class InviteCodeLookupTests
    {
        private static BowlPool Pool(string id, string season, string code = "BOWL", bool archived = false) =>
            new() { Id = id, SeasonId = season, InviteCode = code, IsArchived = archived };

        private static BowlPool? Find(IEnumerable<BowlPool> pools, string code, bool fallback = false, params string[] current) =>
            InviteCodeLookup.Find(pools, code, current.Length == 0 ? new[] { "season-2026" } : current,
                p => p.InviteCode, p => p.SeasonId, p => p.IsArchived, fallback);

        [Fact]
        public void Find_ShouldChooseTheCurrentSeasonsPool_WhenTwoPoolsShareTheCode()
        {
            var lastYear = Pool("old", "season-2025");
            var thisYear = Pool("new", "season-2026");

            Find(new[] { lastYear, thisYear }, "BOWL")!.Id.Should().Be("new");
            Find(new[] { thisYear, lastYear }, "BOWL")!.Id.Should().Be("new");
        }

        [Fact]
        public void Find_ShouldMatchTheCodeIgnoringCaseAndSpaces()
        {
            Find(new[] { Pool("new", "season-2026") }, "  bowl ")!.Id.Should().Be("new");
        }

        [Fact]
        public void Find_ShouldReturnNothing_WhenOnlyAnOlderSeasonHasTheCode()
        {
            Find(new[] { Pool("old", "season-2025") }, "BOWL").Should().BeNull();
        }

        [Fact]
        public void Find_ShouldSkipArchivedPools()
        {
            Find(new[] { Pool("new", "season-2026", archived: true) }, "BOWL").Should().BeNull();
        }

        [Fact]
        public void Find_ShouldReturnNothing_WhenTheCodeIsBlankOrUnknown()
        {
            var pools = new[] { Pool("new", "season-2026") };

            Find(pools, "").Should().BeNull();
            Find(pools, "NOPE").Should().BeNull();
        }

        [Fact]
        public void Find_ShouldUseTheFirstOpenMatch_WhenFallbackIsOnAndNoPoolIsInTheCurrentSeason()
        {
            var odd = Pool("hoops", "2026");

            Find(new[] { odd }, "BOWL", fallback: true, current: "season-guid-xyz")!.Id.Should().Be("hoops");
        }

        [Fact]
        public void Find_ShouldAcceptEitherNameOfTheCurrentSeason()
        {
            // Hoops pools can carry the season's year text instead of its id (BUSINESS-RULES section 6).
            var byYear = Pool("by-year", "2026");
            var lastYear = Pool("old", "2025");

            Find(new[] { lastYear, byYear }, "BOWL", fallback: true, current: new[] { "season-guid", "2026" })!.Id.Should().Be("by-year");
        }
    }
}
