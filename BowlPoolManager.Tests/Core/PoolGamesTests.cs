using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    // A pool's games for the print pages: the pool's chosen games, or every game of its season when it chose none.
    public class PoolGamesTests
    {
        private static BowlGame G(string id, string season) => new BowlGame { Id = id, SeasonId = season };

        [Fact]
        public void Of_ShouldKeepOnlyThePoolsGames_WhenPoolListsGames()
        {
            var pool = new BowlPool { SeasonId = "s1", GameIds = new List<string> { "a", "c" } };

            PoolGames.Of(pool, new[] { G("a", "s1"), G("b", "s1"), G("c", "s1") }).Select(g => g.Id).Should().Equal("a", "c");
        }

        [Fact]
        public void Of_ShouldUseEverySeasonGame_WhenPoolListsNone()
        {
            var pool = new BowlPool { SeasonId = "s1", GameIds = new List<string>() };

            PoolGames.Of(pool, new[] { G("a", "s1"), G("b", "s2") }).Select(g => g.Id).Should().Equal("a");
        }

        [Fact]
        public void Of_ShouldDropGamesOfAnotherSeason_WhenPoolListsThem()
        {
            var pool = new BowlPool { SeasonId = "s1", GameIds = new List<string> { "a", "b" } };

            PoolGames.Of(pool, new[] { G("a", "s1"), G("b", "s2") }).Select(g => g.Id).Should().Equal("a");
        }
    }
}
