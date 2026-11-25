using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    // The blank form shows only teams that are set in advance (upgrade plan 5.4): both teams in a first-round game,
    // the top seed in a quarterfinal, nothing in a semifinal or the championship.
    public class BlankFormSlotsTests
    {
        private static BowlGame G(string id, string home, string away, string? next = null, TournamentRound round = TournamentRound.Round1, int mins = 0) =>
            new BowlGame { Id = id, BowlName = id, TeamHome = home, TeamAway = away, NextGameId = next, IsPlayoff = true, Round = round,
                           StartTime = new DateTime(2026, 12, 20, 12, 0, 0, DateTimeKind.Utc).AddMinutes(mins) };

        [Fact]
        public void SlotsFor_ShouldShowBothTeams_WhenFirstRoundGame()
        {
            var r1 = G("r1", "Tulane", "Texas", "qf");
            var qf = G("qf", "Ohio State", "Winner of r1", "sf", TournamentRound.QuarterFinal, 60);

            BlankFormSlots.SlotsFor(r1, new[] { r1, qf }).Should().Be(("Tulane", "Texas"));
        }

        [Fact]
        public void SlotsFor_ShouldShowOnlyTheSeed_WhenQuarterfinal()
        {
            var r1 = G("r1", "Tulane", "Texas", "qf");
            var qf = G("qf", "Ohio State", "Winner of r1", "sf", TournamentRound.QuarterFinal, 60);

            BlankFormSlots.SlotsFor(qf, new[] { r1, qf }).Should().Be(("Ohio State", (string?)null));
        }

        [Fact]
        public void SlotsFor_ShouldBlankTheFeederSlot_WhenServerAlreadyCopiedARealWinnerForward()
        {
            var r1 = G("r1", "Tulane", "Texas", "qf");
            var qf = G("qf", "Ohio State", "Texas", "sf", TournamentRound.QuarterFinal, 60);

            BlankFormSlots.SlotsFor(qf, new[] { r1, qf }).Should().Be(("Ohio State", (string?)null));
        }

        [Fact]
        public void SlotsFor_ShouldBeBlank_WhenSemifinalOrChampionship()
        {
            var qf1 = G("qf1", "Ohio State", "Winner of r1", "sf", TournamentRound.QuarterFinal);
            var qf2 = G("qf2", "Oregon", "Winner of r2", "sf", TournamentRound.QuarterFinal, 30);
            var sf = G("sf", "Winner of qf1", "Winner of qf2", "ch", TournamentRound.SemiFinal, 90);
            var ch = G("ch", "TBD", "TBD", null, TournamentRound.Championship, 120);
            var all = new[] { qf1, qf2, sf, ch };

            BlankFormSlots.SlotsFor(sf, all).Should().Be(((string?)null, (string?)null));
            BlankFormSlots.SlotsFor(ch, all).Should().Be(((string?)null, (string?)null));
        }
    }
}
