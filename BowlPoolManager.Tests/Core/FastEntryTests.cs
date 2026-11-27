using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>
    /// Fast paper entry: the keyer types one digit per game, reading down the printed form (prototype 4, owner decision
    /// 2026-11-25). 1 = the team on the form's first line, 2 = the second line, 0 = no pick. A regular bowl lists the
    /// away team first; a playoff game lists its home team first.
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
        public void Apply_ShouldPickTheTeamOnTopOfThePrintedForm_WhenAPlayoffGameIsKeyedOne()
        {
            // The printed form puts a playoff game's home team (the higher seed) on top, so 1 is the home team there.
            var result = Apply("0012");

            result.Picks["r1a"].Should().Be("Miami");
            result.Picks["r1b"].Should().Be("James Madison");
        }

        [Fact]
        public void Apply_ShouldMatchThePrintedFormLineByLine_WhenEveryGameIsKeyedOne()
        {
            // Regression (production, 2026-11-25): keying 1 for the top line of a playoff game picked the other team.
            var games = Games();
            var ordered = Ordered(games);
            var picks = FastEntry.Apply(ordered, games, "11111").Picks;

            foreach (var game in ordered.Where(g => g.IsPlayoff))
            {
                var top = BlankFormSlots.SlotsFor(game, games).Top;
                var expected = top.Team ?? picks[games.Single(g => g.BowlName == top.FeederBowl).Id];
                picks[game.Id].Should().Be(expected, $"1 should pick the top line of {game.Id}");
            }
            picks["b1"].Should().Be("Toledo");
            picks["b2"].Should().Be("Utah");
        }

        [Theory]
        [InlineData('1')]
        [InlineData('2')]
        public void Apply_ShouldMatchThePrintedFormLineByLine_WhenSeedsAndFeedersShareAGame(char digit)
        {
            // qfa: a seed at home and a feeder away; qfb: a seed away and a feeder at home; sf: two feeders.
            var games = new List<BowlGame>
            {
                Game("r1a", "Navy", "Army", 10, "qfa", true),
                Game("r1b", "Tulane", "Memphis", 11, "qfb", true),
                Game("qfa", "Winner of R1A", "Ohio State", 12, "sf", true),
                Game("qfb", "Georgia", "Winner of R1B", 13, "sf", true),
                Game("sf", "Winner of QFB", "Winner of QFA", 14, null, true)
            };
            var ordered = Ordered(games);
            var picks = FastEntry.Apply(ordered, games, new string(digit, ordered.Count)).Picks;

            foreach (var game in ordered)
            {
                var (top, bottom) = BlankFormSlots.SlotsFor(game, games);
                var line = digit == '1' ? top : bottom;
                var expected = line.Team ?? picks[games.Single(g => g.BowlName == line.FeederBowl).Id];
                picks[game.Id].Should().Be(expected, $"{digit} should pick line {digit} of {game.Id} on the printed form");
            }
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public void HomeOnTop_ShouldPutTheHomeTeamFirst_OnlyWhenTheGameIsAPlayoffGame(bool playoff, bool expected)
        {
            BlankFormSlots.HomeOnTop(Game("g", "Away", "Home", 10, null, playoff)).Should().Be(expected);
        }

        [Fact]
        public void Apply_ShouldPickTheTeamTheEarlierDigitsLeadTo_WhenTheGameIsAPlayoffSlot()
        {
            // r1a = 1 (Miami, home), r1b = 1 (Oregon, home); qf: 1 = home (winner of r1a), 2 = away (winner of r1b).
            Apply("00112").Picks["qf"].Should().Be("Oregon");
            Apply("00111").Picks["qf"].Should().Be("Miami");
        }

        [Fact]
        public void Apply_ShouldListTheGameAsNotKnown_WhenItsTeamDependsOnABlankGame()
        {
            var result = Apply("00102");

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

            var result = FastEntry.Apply(Ordered(games), games, "22");

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

            var result = FastEntry.Apply(Ordered(games), games, "10112", stored);

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

            var result = FastEntry.Apply(ordered, games, "0011", stored);

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
            // qf keyed 2 = the away slot = the winner of r1b; r1b changes from Oregon to James Madison.
            var result = Rekey("12112", "12122");

            result.Sequence.Should().Be("12120");
            result.ClearedGameIds.Should().Equal("qf");
        }

        [Fact]
        public void ClearDependents_ShouldKeepALaterPlayoffDigit_WhenItsTeamCameFromTheOtherFeeder()
        {
            // qf keyed 2 takes the winner of r1b; changing r1a does not touch it.
            var result = Rekey("12112", "12212");

            result.Sequence.Should().Be("12212");
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

            var result = FastEntry.ClearDependents(Ordered(games), games, "2", "22", null);

            result.Sequence.Should().Be("22");
            result.ClearedGameIds.Should().BeEmpty();
        }

        [Fact]
        public void ClearDependents_ShouldChangeNothing_WhenOnlyARegularBowlChanges()
        {
            var result = Rekey("12112", "22112");

            result.Sequence.Should().Be("22112");
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
            var result = Rekey("12112", "12211");

            result.Sequence.Should().Be("12211");
            result.ClearedGameIds.Should().BeEmpty();
        }

        [Fact]
        public void ClearDependents_ShouldListASavedPick_WhenItsFeederDigitChanges()
        {
            var stored = new Dictionary<string, string> { ["qf"] = "Miami" };

            var result = Rekey("00100", "00200", stored);

            result.ClearedGameIds.Should().Equal("qf");
        }

        [Fact]
        public void ClearDependents_ShouldClearEveryLaterRound_WhenTheChainIsLong()
        {
            var games = Games();
            games.Single(g => g.Id == "qf").NextGameId = "sf";
            games.Add(Game("sf", "Winner of QF", "Ohio State", 20, null, true));

            var result = FastEntry.ClearDependents(Ordered(games), games, "121122", "121222", null);

            result.Sequence.Should().Be("121200");
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
