using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    // The printed form lists each round top to bottom by kickoff, the same order as fast paper entry (owner, 2026-11-25),
    // so a paper sheet keys straight in. The blanks name their feeder game, so the cards need not sit beside it.
    public class PlayoffColumnsTests
    {
        private static BowlGame G(string id, TournamentRound round, string? next, int mins) =>
            new BowlGame { Id = id, BowlName = id, IsPlayoff = true, Round = round, NextGameId = next,
                           StartTime = new DateTime(2026, 12, 20, 12, 0, 0, DateTimeKind.Utc).AddMinutes(mins) };

        [Fact]
        public void Build_ShouldOrderEachRoundByKickoff_WhenTreeOrderDiffers()
        {
            var ch = G("ch", TournamentRound.Championship, null, 500);
            var sfA = G("sfA", TournamentRound.SemiFinal, "ch", 300);
            var sfB = G("sfB", TournamentRound.SemiFinal, "ch", 400);
            var qf1 = G("qf1", TournamentRound.QuarterFinal, "sfB", 100);   // feeds the later semifinal but kicks off first
            var qf2 = G("qf2", TournamentRound.QuarterFinal, "sfA", 200);
            var r1a = G("r1a", TournamentRound.Round1, "qf2", 10);
            var r1b = G("r1b", TournamentRound.Round1, "qf1", 20);

            var cols = PlayoffColumns.Build(new[] { r1b, ch, qf2, sfB, r1a, qf1, sfA });

            cols.Championship.Select(g => g.Id).Should().Equal("ch");
            cols.SemiFinals.Select(g => g.Id).Should().Equal("sfA", "sfB");
            cols.Quarterfinals.Select(g => g.Id).Should().Equal("qf1", "qf2");
            cols.FirstRound.Select(g => g.Id).Should().Equal("r1a", "r1b");
        }

        [Fact]
        public void Build_ShouldReadInFastEntryOrder_WhenColumnsAreReadLeftToRight()
        {
            var games = new[]
            {
                G("ch", TournamentRound.Championship, null, 500), G("sf", TournamentRound.SemiFinal, "ch", 300),
                G("qf2", TournamentRound.QuarterFinal, "sf", 200), G("qf1", TournamentRound.QuarterFinal, "sf", 100),
                G("r1b", TournamentRound.Round1, "qf2", 20), G("r1a", TournamentRound.Round1, "qf1", 10),
            };

            var cols = PlayoffColumns.Build(games);
            var read = cols.FirstRound.Concat(cols.Quarterfinals).Concat(cols.SemiFinals).Concat(cols.Championship);

            read.Select(g => g.Id).Should().Equal(GameOrder.ForEntry(games).Select(g => g.Id));
        }

        [Fact]
        public void Build_ShouldBreakTiesById_WhenKickoffsAreEqual()
        {
            var b = G("b", TournamentRound.Round1, null, 0);
            var a = G("a", TournamentRound.Round1, null, 0);

            PlayoffColumns.Build(new[] { b, a }).FirstRound.Select(g => g.Id).Should().Equal("a", "b");
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
