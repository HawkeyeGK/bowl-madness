using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    // The printed tree lists each round top to bottom in the order its games feed the next round.
    public class PlayoffColumnsTests
    {
        private static BowlGame G(string id, TournamentRound round, string? next, int mins) =>
            new BowlGame { Id = id, BowlName = id, IsPlayoff = true, Round = round, NextGameId = next,
                           StartTime = new DateTime(2026, 12, 20, 12, 0, 0, DateTimeKind.Utc).AddMinutes(mins) };

        [Fact]
        public void Build_ShouldOrderEachRoundByTheGameItFeeds_WhenKickoffOrderDiffers()
        {
            var ch = G("ch", TournamentRound.Championship, null, 500);
            var sfA = G("sfA", TournamentRound.SemiFinal, "ch", 400);
            var sfB = G("sfB", TournamentRound.SemiFinal, "ch", 300);   // earlier kickoff, so it is listed first
            var qf1 = G("qf1", TournamentRound.QuarterFinal, "sfB", 200);
            var qf2 = G("qf2", TournamentRound.QuarterFinal, "sfA", 100);
            var r1a = G("r1a", TournamentRound.Round1, "qf2", 20);
            var r1b = G("r1b", TournamentRound.Round1, "qf1", 10);

            var cols = PlayoffColumns.Build(new[] { r1a, r1b, qf1, qf2, sfA, sfB, ch });

            cols.Championship.Select(g => g.Id).Should().Equal("ch");
            cols.SemiFinals.Select(g => g.Id).Should().Equal("sfB", "sfA");
            cols.Quarterfinals.Select(g => g.Id).Should().Equal("qf1", "qf2");
            cols.FirstRound.Select(g => g.Id).Should().Equal("r1b", "r1a");
        }

        [Fact]
        public void Build_ShouldIgnoreStandardBowls()
        {
            var std = new BowlGame { Id = "std", IsPlayoff = false };

            var cols = PlayoffColumns.Build(new[] { std });

            cols.FirstRound.Should().BeEmpty();
            cols.Championship.Should().BeEmpty();
        }
    }
}
