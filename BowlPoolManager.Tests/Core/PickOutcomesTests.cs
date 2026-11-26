using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    public class PickOutcomesTests
    {
        private static BowlGame Game(GameStatus status, int? home, int? away) => new()
        {
            Id = "g1", TeamHome = "Iowa", TeamAway = "Vanderbilt", Status = status, TeamHomeScore = home, TeamAwayScore = away
        };

        [Fact]
        public void For_ShouldBeCorrect_WhenTheFinalGameWasWonByThePick()
        {
            PickOutcomes.For(Game(GameStatus.Final, 24, 17), "Iowa").Should().Be(PickOutcome.Correct);
        }

        [Fact]
        public void For_ShouldBeWrong_WhenTheFinalGameWasWonByTheOtherTeam()
        {
            PickOutcomes.For(Game(GameStatus.Final, 24, 17), "Vanderbilt").Should().Be(PickOutcome.Wrong);
        }

        [Fact]
        public void For_ShouldMatchIgnoringCase()
        {
            PickOutcomes.For(Game(GameStatus.Final, 24, 17), "IOWA").Should().Be(PickOutcome.Correct);
        }

        [Theory]
        [InlineData(GameStatus.Scheduled)]
        [InlineData(GameStatus.InProgress)]
        public void For_ShouldBePending_WhenTheGameIsNotFinal(GameStatus status)
        {
            PickOutcomes.For(Game(status, 7, 3), "Iowa").Should().Be(PickOutcome.Pending);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData(7, null)]
        [InlineData(null, 7)]
        [InlineData(21, 21)]
        public void For_ShouldBePending_WhenAFinalGameHasNoDecidedWinner(int? home, int? away)
        {
            // BUSINESS-RULES section 4: a Final game counts only with both scores and a winner.
            PickOutcomes.For(Game(GameStatus.Final, home, away), "Iowa").Should().Be(PickOutcome.Pending);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void For_ShouldBeNoPick_WhenTheEntryHasNoPickForTheGame(string? pick)
        {
            PickOutcomes.For(Game(GameStatus.Final, 24, 17), pick).Should().Be(PickOutcome.NoPick);
        }
    }
}
