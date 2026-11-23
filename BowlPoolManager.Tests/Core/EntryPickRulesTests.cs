using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>
    /// Issue 128: which teams an entry may pick in each game, given the entry's own earlier picks.
    /// Bracket used below: r1a (Miami v Texas A&amp;M) and r1b (Oregon v James Madison) feed qf; qf home is the
    /// winner of r1a and qf away is the winner of r1b; qf feeds sf, whose home is the seed "Indiana".
    /// </summary>
    public class EntryPickRulesTests
    {
        private static BowlGame Game(string id, string home, string away, int hour, string? next = null, bool playoff = true) => new()
        {
            Id = id, BowlName = id.ToUpperInvariant(), TeamHome = home, TeamAway = away, NextGameId = next, IsPlayoff = playoff,
            StartTime = new DateTime(2026, 12, 20, hour, 0, 0, DateTimeKind.Utc)
        };

        private static List<BowlGame> Bracket() => new()
        {
            Game("r1a", "Miami", "Texas A&M", 12, "qf"),
            Game("r1b", "Oregon", "James Madison", 13, "qf"),
            Game("qf", "Winner of R1A", "Winner of R1B", 18, "sf"),
            Game("sf", "Indiana", "Winner of QF", 23),
            Game("bowl", "Toledo", "Pitt", 14, null, playoff: false)
        };

        private static Dictionary<string, string> Picks(params (string game, string team)[] picks) =>
            picks.ToDictionary(p => p.game, p => p.team);

        [Fact]
        public void Resolve_ShouldLeaveLaterSlotsUnresolved_WhenFeederHasNoPick()
        {
            var m = EntryPickRules.Resolve(Bracket(), Picks());

            m["qf"].Home.Should().BeNull();
            m["qf"].Away.Should().BeNull();
            m["qf"].HomeLabel.Should().Be("Winner of R1A");
            m["sf"].Home.Should().Be("Indiana");
            m["sf"].Away.Should().BeNull();
        }

        [Fact]
        public void Resolve_ShouldFillTheSlot_WhenFeederPickIsOneOfItsTeams()
        {
            var m = EntryPickRules.Resolve(Bracket(), Picks(("r1a", "Miami"), ("r1b", "Oregon")));

            m["qf"].Home.Should().Be("Miami");
            m["qf"].Away.Should().Be("Oregon");
        }

        [Fact]
        public void Resolve_ShouldIgnoreAFeederPick_WhenItIsNotOneOfThatGamesTeams()
        {
            var m = EntryPickRules.Resolve(Bracket(), Picks(("r1a", "Winner of R1B"), ("r1b", "Oregon")));

            m["qf"].Home.Should().BeNull();
            m["qf"].Away.Should().Be("Oregon");
        }

        [Fact]
        public void Resolve_ShouldUseTheRealWinner_WhenTheFeederIsAlreadyDecided()
        {
            var games = Bracket();
            games.First(g => g.Id == "qf").TeamHome = "Texas A&M"; // propagated real result

            var m = EntryPickRules.Resolve(games, Picks(("r1a", "Miami")));

            m["qf"].Home.Should().Be("Texas A&M");
        }

        [Fact]
        public void Resolve_ShouldCarryAPickThroughTwoRounds_WhenEveryEarlierPickIsValid()
        {
            var m = EntryPickRules.Resolve(Bracket(), Picks(("r1a", "Miami"), ("r1b", "Oregon"), ("qf", "Oregon")));

            m["sf"].Away.Should().Be("Oregon");
        }

        [Fact]
        public void Resolve_ShouldNotCarryAStalePick_WhenAnEarlierPickChanged()
        {
            // The player picked Miami, then Miami in the quarterfinal, then changed the first round to Texas A&M.
            var m = EntryPickRules.Resolve(Bracket(), Picks(("r1a", "Texas A&M"), ("r1b", "Oregon"), ("qf", "Miami")));

            m["qf"].Home.Should().Be("Texas A&M");
            m["sf"].Away.Should().BeNull(); // the quarterfinal pick "Miami" is not valid, so nothing flows on
        }

        [Fact]
        public void Resolve_ShouldMatchTeamNamesIgnoringCase()
        {
            var m = EntryPickRules.Resolve(Bracket(), Picks(("r1a", "miami")));

            m["qf"].Home.Should().Be("Miami"); // the game's own spelling
        }

        [Fact]
        public void Resolve_ShouldNameTheFeeder_WhenASlotWaitsForOne()
        {
            var m = EntryPickRules.Resolve(Bracket(), Picks());

            m["qf"].HomeWaitsFor.Should().Be("R1A");
            m["qf"].AwayWaitsFor.Should().Be("R1B");
            m["sf"].HomeWaitsFor.Should().BeNull();
            m["sf"].AwayWaitsFor.Should().Be("QF");
        }

        [Fact]
        public void IsValidPick_ShouldBeFalse_WhenThePickIsPlaceholderTextOrUnreachable()
        {
            var m = EntryPickRules.Resolve(Bracket(), Picks(("r1a", "Miami")));

            EntryPickRules.IsValidPick(m["qf"], "Winner of R1B").Should().BeFalse();
            EntryPickRules.IsValidPick(m["qf"], "TBD").Should().BeFalse();
            EntryPickRules.IsValidPick(m["qf"], "Oregon").Should().BeFalse(); // slot not known yet
            EntryPickRules.IsValidPick(m["qf"], "Miami").Should().BeTrue();
            EntryPickRules.IsValidPick(m["qf"], "").Should().BeFalse();
            EntryPickRules.IsValidPick(m["qf"], null).Should().BeFalse();
        }

        [Fact]
        public void ClearInvalid_ShouldRemoveDependentPicks_WhenAnEarlierPickChanged()
        {
            var picks = Picks(("r1a", "Texas A&M"), ("r1b", "Oregon"), ("qf", "Miami"), ("sf", "Miami"), ("bowl", "Toledo"));

            var result = EntryPickRules.ClearInvalid(Bracket(), picks);

            result.Picks.Should().BeEquivalentTo(Picks(("r1a", "Texas A&M"), ("r1b", "Oregon"), ("bowl", "Toledo")));
            result.ClearedGameIds.Should().BeEquivalentTo(new[] { "qf", "sf" });
        }

        [Fact]
        public void ClearInvalid_ShouldRemoveJunkPlaceholderPicks()
        {
            var picks = Picks(("r1a", "Miami"), ("qf", "Winner of R1B"));

            var result = EntryPickRules.ClearInvalid(Bracket(), picks);

            result.Picks.Should().BeEquivalentTo(Picks(("r1a", "Miami")));
            result.ClearedGameIds.Should().Equal("qf");
        }

        [Fact]
        public void ClearInvalid_ShouldChangeNothing_WhenEveryPickIsValid()
        {
            var picks = Picks(("r1a", "Miami"), ("r1b", "Oregon"), ("qf", "Oregon"), ("sf", "Indiana"));

            var result = EntryPickRules.ClearInvalid(Bracket(), picks);

            result.Picks.Should().BeEquivalentTo(picks);
            result.ClearedGameIds.Should().BeEmpty();
        }

        [Fact]
        public void ClearInvalid_ShouldNotChangeTheCallersDictionary()
        {
            var picks = Picks(("r1a", "Texas A&M"), ("qf", "Miami"));

            EntryPickRules.ClearInvalid(Bracket(), picks);

            picks.Should().ContainKey("qf");
        }

        [Fact]
        public void ClearInvalid_ShouldDropEmptyPicksWithoutCountingThemAsCleared()
        {
            var picks = Picks(("r1a", ""), ("r1b", "Oregon"));

            var result = EntryPickRules.ClearInvalid(Bracket(), picks);

            result.Picks.Should().BeEquivalentTo(Picks(("r1b", "Oregon")));
            result.ClearedGameIds.Should().BeEmpty();
        }

        [Fact]
        public void ClearInvalid_ShouldLeaveAPickForAnUnknownGameAlone()
        {
            var picks = Picks(("gone", "X"), ("r1a", "Miami"));

            var result = EntryPickRules.ClearInvalid(Bracket(), picks);

            result.Picks.Should().BeEquivalentTo(picks);
            result.ClearedGameIds.Should().BeEmpty();
        }

        [Fact]
        public void CountValid_ShouldCountOnlyPicksForPoolGamesThatAreReachable()
        {
            var picks = Picks(("r1a", "Miami"), ("qf", "Miami"), ("sf", "Winner of QF"), ("bowl", "Toledo"), ("gone", "X"));
            var games = Bracket();

            // r1a ok, qf ok? qf away unknown but home Miami -> ok, sf junk, bowl ok, gone not a game.
            EntryPickRules.CountValid(games, games.Select(g => g.Id).ToList(), picks).Should().Be(3);
        }

        [Fact]
        public void CountValid_ShouldIgnoreGamesOutsideThePool()
        {
            var picks = Picks(("r1a", "Miami"), ("bowl", "Toledo"));

            EntryPickRules.CountValid(Bracket(), new[] { "r1a" }, picks).Should().Be(1);
        }

        [Fact]
        public void Resolve_ShouldNotLoopForever_WhenGamesFeedEachOther()
        {
            var a = Game("a", "Winner of B", "X", 12, "b");
            var b = Game("b", "Winner of A", "Y", 13, "a");

            var act = () => EntryPickRules.Resolve(new List<BowlGame> { a, b }, Picks());

            act.Should().NotThrow();
        }
    }
}
