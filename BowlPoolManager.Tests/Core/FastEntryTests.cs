using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>
    /// Fast paper entry: the keyer types one digit per game, reading down the printed form (prototype 4, owner decision
    /// 2026-11-25). 1 = the team listed first (away), 2 = the team listed second (home), 0 = no pick.
    /// Bracket used below: b1 and b2 are regular bowls; r1a and r1b feed qf; qf home is the winner of r1a (earlier
    /// kickoff) and qf away is the winner of r1b. Entry order: b1, b2, r1a, r1b, qf.
    /// </summary>
    public class FastEntryTests
    {
        private static BowlGame Game(string id, string away, string home, int hour, string? next = null, bool playoff = false) => new()
        {
            Id = id, BowlName = id.ToUpperInvariant(), TeamAway = away, TeamHome = home, NextGameId = next, IsPlayoff = playoff,
            Round = playoff ? TournamentRound.Round1 : TournamentRound.Standard,
            StartTime = new DateTime(2026, 12, 20, hour, 0, 0, DateTimeKind.Utc)
        };

        private static List<BowlGame> Games() => new()
        {
            Game("b1", "Toledo", "Pitt", 10),
            Game("b2", "Utah", "Wake Forest", 11),
            Game("r1a", "Texas A&M", "Miami", 12, "qf", true),
            Game("r1b", "James Madison", "Oregon", 13, "qf", true),
            Game("qf", "Winner of R1B", "Winner of R1A", 18, null, true)
        };

        private static List<BowlGame> Ordered(List<BowlGame> games) => GameOrder.ForEntry(games);

        private static SequenceResult Apply(string? sequence)
        {
            var games = Games();
            return FastEntry.Apply(Ordered(games), games, sequence);
        }

        [Fact]
        public void Apply_ShouldPickTheFirstOrSecondTeam_WhenTheDigitIsOneOrTwo()
        {
            var result = Apply("12");

            result.Picks["b1"].Should().Be("Toledo");
            result.Picks["b2"].Should().Be("Wake Forest");
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-")]
        [InlineData(" ")]
        public void Apply_ShouldLeaveTheGameBlank_WhenTheDigitMeansNoPick(string blank)
        {
            var result = Apply(blank + "1");

            result.Picks.Should().NotContainKey("b1");
            result.Picks["b2"].Should().Be("Utah");
            result.Sequence.Should().Be("01");
        }

        [Fact]
        public void Apply_ShouldDropOtherCharacters_WhenTheKeyerTypesThem()
        {
            var result = Apply("1x3,2");

            result.Sequence.Should().Be("12");
            result.Picks["b2"].Should().Be("Wake Forest");
        }

        [Fact]
        public void Apply_ShouldIgnoreDigits_WhenTheyRunPastTheLastGame()
        {
            var result = Apply("1111111");

            result.Sequence.Should().HaveLength(5);
        }

        [Fact]
        public void Apply_ShouldReturnNoPicks_WhenTheSequenceIsEmptyOrNull()
        {
            Apply(null).Picks.Should().BeEmpty();
            Apply("").Sequence.Should().BeEmpty();
        }

        [Fact]
        public void Apply_ShouldPickTheTeamTheEarlierDigitsLeadTo_WhenTheGameIsAPlayoffSlot()
        {
            // r1a = 2 (Miami), r1b = 2 (Oregon); qf: 1 = the first team listed (away: winner of r1b).
            Apply("00221").Picks["qf"].Should().Be("Oregon");
            Apply("00222").Picks["qf"].Should().Be("Miami");
        }

        [Fact]
        public void Apply_ShouldListTheGameAsNotKnown_WhenItsTeamDependsOnABlankGame()
        {
            var result = Apply("00201");

            result.Picks.Should().NotContainKey("qf");
            result.NotKnownGameIds.Should().Equal("qf");
        }

        [Fact]
        public void Apply_ShouldFillALaterSlot_WhenItsFeederIsKeyedLaterInTheOrder()
        {
            // A feeder that kicks off after the game it feeds still fills it.
            var games = new List<BowlGame>
            {
                Game("next", "Winner of FEED", "Ohio State", 10, null, true),
                Game("feed", "Navy", "Army", 12, "next", true)
            };

            var result = FastEntry.Apply(Ordered(games), games, "11");

            result.Picks["feed"].Should().Be("Navy");
            result.Picks["next"].Should().Be("Navy");
        }

        [Fact]
        public void Apply_ShouldKeepAStoredPick_WhenItsGameIsBlankInTheDigits()
        {
            // A saved pick the digits cannot show (a team already knocked out, say) is kept until the game is keyed.
            var games = Games();
            var stored = new Dictionary<string, string> { ["qf"] = "Busted Team" };

            var result = FastEntry.Apply(Ordered(games), games, "0000", stored);

            result.Picks["qf"].Should().Be("Busted Team");
        }

        [Fact]
        public void Apply_ShouldReplaceAStoredPick_WhenItsGameIsKeyed()
        {
            var games = Games();
            var stored = new Dictionary<string, string> { ["qf"] = "Busted Team", ["b1"] = "Pitt" };

            var result = FastEntry.Apply(Ordered(games), games, "10221", stored);

            result.Picks["qf"].Should().Be("Oregon");
            result.Picks["b1"].Should().Be("Toledo");
        }

        [Fact]
        public void Apply_ShouldUseAStoredFeederPick_WhenTheFeederIsNotInThePool()
        {
            // The pool lists qf but not r1a; the entry still holds a pick for r1a, which fills qf's home slot.
            var games = Games();
            var ordered = Ordered(games).Where(g => g.Id != "r1a").ToList();
            var stored = new Dictionary<string, string> { ["r1a"] = "Miami" };

            var result = FastEntry.Apply(ordered, games, "0022", stored);

            result.Picks["qf"].Should().Be("Miami");
            result.Picks["r1a"].Should().Be("Miami");
        }

        // ── Changing an earlier digit clears the later ones that built on it (owner decision 2026-11-25) ──

        private static RekeyResult Rekey(string oldSequence, string newSequence, Dictionary<string, string>? stored = null)
        {
            var games = Games();
            return FastEntry.ClearDependents(Ordered(games), games, oldSequence, newSequence, stored);
        }

        [Fact]
        public void ClearDependents_ShouldClearALaterPlayoffDigit_WhenItsTeamCameThroughTheChangedGame()
        {
            // qf keyed 1 = the away slot = the winner of r1b; r1b changes from Oregon to James Madison.
            var result = Rekey("12221", "12211");

            result.Sequence.Should().Be("12210");
            result.ClearedGameIds.Should().Equal("qf");
        }

        [Fact]
        public void ClearDependents_ShouldKeepALaterPlayoffDigit_WhenItsTeamCameFromTheOtherFeeder()
        {
            // qf keyed 1 takes the winner of r1b; changing r1a does not touch it.
            var result = Rekey("12221", "12121");

            result.Sequence.Should().Be("12121");
            result.ClearedGameIds.Should().BeEmpty();
        }

        [Fact]
        public void ClearDependents_ShouldKeepADigit_WhenItsFeederIsKeyedLaterInTheOrder()
        {
            // The game a feeder fills kicks off first: its digit had no team yet, so keying the feeder loses nothing.
            var games = new List<BowlGame>
            {
                Game("next", "Winner of FEED", "Ohio State", 10, null, true),
                Game("feed", "Navy", "Army", 12, "next", true)
            };

            var result = FastEntry.ClearDependents(Ordered(games), games, "1", "11", null);

            result.Sequence.Should().Be("11");
            result.ClearedGameIds.Should().BeEmpty();
        }

        [Fact]
        public void ClearDependents_ShouldChangeNothing_WhenOnlyARegularBowlChanges()
        {
            var result = Rekey("12221", "22221");

            result.Sequence.Should().Be("22221");
            result.ClearedGameIds.Should().BeEmpty();
        }

        [Fact]
        public void ClearDependents_ShouldChangeNothing_WhenTheKeyerTypesTheNextDigit()
        {
            var result = Rekey("122", "1222");

            result.Sequence.Should().Be("1222");
            result.ClearedGameIds.Should().BeEmpty();
        }

        [Fact]
        public void ClearDependents_ShouldKeepADigit_WhenThatGameChangedInTheSameEdit()
        {
            // Pasting a whole new line changes the feeder and the later game together: both are what the keyer meant.
            var result = Rekey("12221", "12122");

            result.Sequence.Should().Be("12122");
            result.ClearedGameIds.Should().BeEmpty();
        }

        [Fact]
        public void ClearDependents_ShouldListASavedPick_WhenItsFeederDigitChanges()
        {
            var stored = new Dictionary<string, string> { ["qf"] = "Miami" };

            var result = Rekey("00200", "00100", stored);

            result.ClearedGameIds.Should().Equal("qf");
        }

        [Fact]
        public void ClearDependents_ShouldClearEveryLaterRound_WhenTheChainIsLong()
        {
            var games = Games();
            games.Single(g => g.Id == "qf").NextGameId = "sf";
            games.Add(Game("sf", "Winner of QF", "Ohio State", 20, null, true));

            var result = FastEntry.ClearDependents(Ordered(games), games, "122211", "122111", null);

            result.Sequence.Should().Be("122100");
            result.ClearedGameIds.Should().Equal("qf", "sf");
        }

        [Fact]
        public void SequenceFor_ShouldGiveBackTheDigits_WhenPicksCameFromASequence()
        {
            var games = Games();
            var picks = FastEntry.Apply(Ordered(games), games, "12221").Picks;

            FastEntry.SequenceFor(Ordered(games), games, picks).Should().Be("12221");
        }

        [Fact]
        public void SequenceFor_ShouldUseZeroAndTrimTheEnd_WhenGamesAreBlank()
        {
            var games = Games();
            var picks = new Dictionary<string, string> { ["b2"] = "Utah" };

            FastEntry.SequenceFor(Ordered(games), games, picks).Should().Be("01");
        }

        [Fact]
        public void SequenceFor_ShouldUseZero_WhenAStoredPickIsNotOneOfTheTeams()
        {
            var games = Games();
            var picks = new Dictionary<string, string> { ["b1"] = "Not A Team", ["b2"] = "Utah" };

            FastEntry.SequenceFor(Ordered(games), games, picks).Should().Be("01");
        }

        [Fact]
        public void WithDigit_ShouldPadWithZeros_WhenTheGameIsPastTheEnd()
        {
            FastEntry.WithDigit("1", 3, '2').Should().Be("1002");
        }

        [Fact]
        public void WithDigit_ShouldReplaceTheDigit_WhenTheGameIsAlreadyKeyed()
        {
            FastEntry.WithDigit("1212", 1, '1').Should().Be("1112");
        }

        [Fact]
        public void Missing_ShouldListGamesInEntryOrder_WhenTheyHaveNoValidPick()
        {
            var games = Games();
            var picks = new Dictionary<string, string> { ["b1"] = "Toledo", ["r1a"] = "Miami" };

            var missing = FastEntry.Missing(Ordered(games), games, picks);

            missing.Select(g => g.Id).Should().Equal("b2", "r1b", "qf");
        }

        [Fact]
        public void Missing_ShouldCountAPickForAnUnreachableTeamAsMissing_WhenTeamIsNotInTheGame()
        {
            var games = Games();
            var picks = new Dictionary<string, string> { ["b1"] = "Not A Team" };

            FastEntry.Missing(Ordered(games), games, picks).Select(g => g.Id).Should().Contain("b1");
        }

        [Fact]
        public void Missing_ShouldListEveryGame_WhenPicksAreNull()
        {
            var games = Games();

            FastEntry.Missing(Ordered(games), games, null).Should().HaveCount(games.Count);
        }

        [Fact]
        public void Missing_ShouldBeEmpty_WhenEveryGameHasAValidPick()
        {
            Apply("12221").Picks.Should().HaveCount(5);
            var games = Games();
            FastEntry.Missing(Ordered(games), games, Apply("12221").Picks).Should().BeEmpty();
        }
    }
}
