using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    // The form shows only teams that are set in advance (upgrade plan 5.4): both teams in a first-round game,
    // the top seed in a quarterfinal, nothing in a semifinal or the championship. A blank says which game feeds it.
    // Printing an entry fills each blank with the entry's pick for the feeder game and checks the entry's winners.
    public class BlankFormSlotsTests
    {
        private static BowlGame G(string id, string home, string away, string? next = null, TournamentRound round = TournamentRound.Round1, int mins = 0,
                                  int? homeSeed = null, int? awaySeed = null) =>
            new BowlGame { Id = id, BowlName = id + " Bowl", TeamHome = home, TeamAway = away, NextGameId = next, IsPlayoff = true, Round = round,
                           TeamHomeSeed = homeSeed, TeamAwaySeed = awaySeed,
                           StartTime = new DateTime(2026, 12, 20, 12, 0, 0, DateTimeKind.Utc).AddMinutes(mins) };

        [Fact]
        public void SlotsFor_ShouldShowBothTeamsWithSeeds_WhenFirstRoundGame()
        {
            var r1 = G("r1", "Tulane", "Texas", "qf", homeSeed: 9, awaySeed: 8);
            var qf = G("qf", "Ohio State", "Winner of r1 Bowl", "sf", TournamentRound.QuarterFinal, 60, homeSeed: 1);

            var (top, bottom) = BlankFormSlots.SlotsFor(r1, new[] { r1, qf });

            top.Should().Be(new FormSlot("Tulane", 9, null, null, false));
            bottom.Should().Be(new FormSlot("Texas", 8, null, null, false));
        }

        [Fact]
        public void SlotsFor_ShouldShowTheSeedAndNameTheFeeder_WhenQuarterfinal()
        {
            var r1 = G("r1", "Tulane", "Texas", "qf");
            var qf = G("qf", "Ohio State", "Winner of r1 Bowl", "sf", TournamentRound.QuarterFinal, 60, homeSeed: 1);

            var (top, bottom) = BlankFormSlots.SlotsFor(qf, new[] { r1, qf });

            top.Should().Be(new FormSlot("Ohio State", 1, null, null, false));
            bottom.Should().Be(new FormSlot(null, null, "r1 Bowl", null, false));
        }

        [Fact]
        public void SlotsFor_ShouldBlankTheFeederSlot_WhenServerAlreadyCopiedARealWinnerForward()
        {
            var r1 = G("r1", "Tulane", "Texas", "qf");
            var qf = G("qf", "Ohio State", "Texas", "sf", TournamentRound.QuarterFinal, 60, homeSeed: 1, awaySeed: 8);

            var (_, bottom) = BlankFormSlots.SlotsFor(qf, new[] { r1, qf });

            bottom.Team.Should().BeNull();
            bottom.Seed.Should().BeNull();
            bottom.FeederBowl.Should().Be("r1 Bowl");
        }

        [Fact]
        public void SlotsFor_ShouldNameEarlierFeederOnTop_WhenSemifinalOrChampionship()
        {
            var qf1 = G("qf1", "Ohio State", "Winner of r1", "sf", TournamentRound.QuarterFinal);
            var qf2 = G("qf2", "Oregon", "Winner of r2", "sf", TournamentRound.QuarterFinal, 30);
            var sf = G("sf", "Winner of qf1 Bowl", "Winner of qf2 Bowl", "ch", TournamentRound.SemiFinal, 90);
            var ch = G("ch", "TBD", "TBD", null, TournamentRound.Championship, 120);
            var all = new[] { qf1, qf2, sf, ch };

            var (sfTop, sfBottom) = BlankFormSlots.SlotsFor(sf, all);
            sfTop.FeederBowl.Should().Be("qf1 Bowl");
            sfBottom.FeederBowl.Should().Be("qf2 Bowl");
            sfTop.Team.Should().BeNull();

            var (chTop, _) = BlankFormSlots.SlotsFor(ch, all);
            chTop.Team.Should().BeNull();
            chTop.FeederBowl.Should().Be("sf Bowl");
        }

        [Fact]
        public void SlotsFor_ShouldWriteFeederPicksAndCheckTheWinner_WhenPicksGiven()
        {
            var r1 = G("r1", "Tulane", "Texas", "qf");
            var qf = G("qf", "Ohio State", "Winner of r1 Bowl", "sf", TournamentRound.QuarterFinal, 60);
            var picks = new Dictionary<string, string> { ["r1"] = "Texas", ["qf"] = "Texas" };

            var (r1Top, r1Bottom) = BlankFormSlots.SlotsFor(r1, new[] { r1, qf }, picks);
            r1Top.Checked.Should().BeFalse();
            r1Bottom.Checked.Should().BeTrue();

            var (qfTop, qfBottom) = BlankFormSlots.SlotsFor(qf, new[] { r1, qf }, picks);
            qfTop.Checked.Should().BeFalse();
            qfBottom.Written.Should().Be("Texas");
            qfBottom.Shown.Should().Be("Texas");
            qfBottom.Checked.Should().BeTrue();
        }

        [Fact]
        public void SlotsFor_ShouldLeaveBlankEmpty_WhenNoPickForFeeder()
        {
            var r1 = G("r1", "Tulane", "Texas", "qf");
            var qf = G("qf", "Ohio State", "Winner of r1 Bowl", "sf", TournamentRound.QuarterFinal, 60);
            var picks = new Dictionary<string, string> { ["qf"] = "Ohio State" };

            var (qfTop, qfBottom) = BlankFormSlots.SlotsFor(qf, new[] { r1, qf }, picks);

            qfTop.Checked.Should().BeTrue();
            qfBottom.Written.Should().BeNull();
            qfBottom.Checked.Should().BeFalse();
        }

        [Fact]
        public void SlotsFor_ShouldPutTheFeederOnTop_WhenTheSeedIsTheAwayTeam()
        {
            var r1 = G("r1", "Tulane", "Texas", "qf");
            var qf = G("qf", "Winner of r1 Bowl", "Ohio State", "sf", TournamentRound.QuarterFinal, 60, awaySeed: 1);
            var picks = new Dictionary<string, string> { ["r1"] = "Tulane", ["qf"] = "Tulane" };

            var (top, bottom) = BlankFormSlots.SlotsFor(qf, new[] { r1, qf }, picks);

            top.Should().Be(new FormSlot(null, null, "r1 Bowl", "Tulane", true));
            bottom.Should().Be(new FormSlot("Ohio State", 1, null, null, false));
        }

        [Fact]
        public void SlotsFor_ShouldWriteBothFeederPicksAndCheckOne_WhenTwoFeeders()
        {
            var qf1 = G("qf1", "Ohio State", "Texas", "sf", TournamentRound.QuarterFinal);
            var qf2 = G("qf2", "Oregon", "Indiana", "sf", TournamentRound.QuarterFinal, 30);
            var sf = G("sf", "Winner of qf1 Bowl", "Winner of qf2 Bowl", "ch", TournamentRound.SemiFinal, 90);
            var picks = new Dictionary<string, string> { ["qf1"] = "Texas", ["qf2"] = "Oregon", ["sf"] = "oregon" };

            var (top, bottom) = BlankFormSlots.SlotsFor(sf, new[] { qf1, qf2, sf }, picks);

            top.Should().Be(new FormSlot(null, null, "qf1 Bowl", "Texas", false));
            bottom.Should().Be(new FormSlot(null, null, "qf2 Bowl", "Oregon", true));
        }

        [Fact]
        public void IsPicked_ShouldMatchIgnoringCase_WhenTheEntryPickedTheTeam()
        {
            var bowl = new BowlGame { Id = "b", TeamHome = "Utah", TeamAway = "Nebraska" };
            var picks = new Dictionary<string, string> { ["b"] = "UTAH" };

            BlankFormSlots.IsPicked(bowl, "Utah", picks).Should().BeTrue();
            BlankFormSlots.IsPicked(bowl, "Nebraska", picks).Should().BeFalse();
            BlankFormSlots.IsPicked(bowl, "Utah", null).Should().BeFalse();
            BlankFormSlots.IsPicked(bowl, null, picks).Should().BeFalse();
        }

        [Fact]
        public void PickFor_ShouldReturnTheEntrysPick_WhenPresent()
        {
            var ch = G("ch", "TBD", "TBD", null, TournamentRound.Championship);

            BlankFormSlots.PickFor(ch, new Dictionary<string, string> { ["ch"] = "Oregon" }).Should().Be("Oregon");
            BlankFormSlots.PickFor(ch, new Dictionary<string, string> { ["ch"] = " " }).Should().BeNull();
            BlankFormSlots.PickFor(ch, null).Should().BeNull();
        }
    }
}
