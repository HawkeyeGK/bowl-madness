using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    public class PickLineTests
    {
        private static BowlGame Game(GameStatus status, int? home = null, int? away = null) => new()
        {
            Id = "g1", TeamHome = "Iowa", TeamAway = "Utah", Status = status, TeamHomeScore = home, TeamAwayScore = away, PointValue = 3
        };

        [Fact]
        public void For_ShouldShowThePointsEarned_WhenThePickWasRight()
        {
            var line = PickLine.For(Game(GameStatus.Final, 24, 10), "Iowa", "Sally");

            line.Text.Should().Be("Sally picked Iowa ✓ +3");
            line.Outcome.Should().Be(PickOutcome.Correct);
        }

        [Fact]
        public void For_ShouldMarkWrong_WhenThePickLost()
        {
            var line = PickLine.For(Game(GameStatus.Final, 24, 10), "Utah", null);

            line.Text.Should().Be("Picked Utah ✗");
            line.Outcome.Should().Be(PickOutcome.Wrong);
        }

        [Fact]
        public void For_ShouldStayPending_WhenTheGameIsNotFinal()
        {
            var line = PickLine.For(Game(GameStatus.Scheduled), "Iowa", "Sally");

            line.Text.Should().Be("Sally picked Iowa");
            line.Outcome.Should().Be(PickOutcome.Pending);
        }

        [Fact]
        public void For_ShouldMarkWrong_WhenThePickedTeamIsAlreadyOut()
        {
            var out1 = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "utah" };

            var line = PickLine.For(Game(GameStatus.Scheduled), "Utah", null, out1);

            line.Text.Should().Be("Picked Utah, who is already out");
            line.Outcome.Should().Be(PickOutcome.Wrong);
        }

        [Theory]
        [InlineData("Sally", "Sally made no pick for this game")]
        [InlineData(null, "No pick for this game")]
        public void For_ShouldSayThereIsNoPick_WhenTheEntryHasNone(string? who, string expected)
        {
            var line = PickLine.For(Game(GameStatus.Scheduled), null, who);

            line.Text.Should().Be(expected);
            line.Outcome.Should().Be(PickOutcome.NoPick);
        }
    }
}
