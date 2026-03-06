using Xunit;
using FluentAssertions;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Core
{
    public class BowlGameTests
    {
        [Fact]
        public void WinningTeamName_ShouldReturnHomeTeam_WhenHomeScoreIsHigher()
        {
            var game = new BowlGame
            {
                TeamHome = "Ohio State",
                TeamAway = "Michigan",
                TeamHomeScore = 42,
                TeamAwayScore = 27,
                Status = GameStatus.Final
            };

            game.WinningTeamName.Should().Be("Ohio State");
            game.LosingTeamName.Should().Be("Michigan");
        }

        [Fact]
        public void WinningTeamName_ShouldReturnAwayTeam_WhenAwayScoreIsHigher()
        {
            var game = new BowlGame
            {
                TeamHome = "Ohio State",
                TeamAway = "Michigan",
                TeamHomeScore = 20,
                TeamAwayScore = 35,
                Status = GameStatus.Final
            };

            game.WinningTeamName.Should().Be("Michigan");
            game.LosingTeamName.Should().Be("Ohio State");
        }

        [Fact]
        public void WinningTeamName_ShouldReturnNull_WhenGameIsNotFinal()
        {
            var game = new BowlGame
            {
                TeamHome = "Ohio State",
                TeamAway = "Michigan",
                TeamHomeScore = 14,
                TeamAwayScore = 7,
                Status = GameStatus.InProgress
            };

            game.WinningTeamName.Should().BeNull();
            game.LosingTeamName.Should().BeNull();
        }

        [Fact]
        public void WinningTeamName_ShouldReturnNull_WhenScoresAreTied()
        {
            var game = new BowlGame
            {
                TeamHome = "Ohio State",
                TeamAway = "Michigan",
                TeamHomeScore = 21,
                TeamAwayScore = 21,
                Status = GameStatus.Final
            };

            game.WinningTeamName.Should().BeNull();
            game.LosingTeamName.Should().BeNull();
        }

        [Fact]
        public void WinningTeamName_ShouldHandleNullScores()
        {
            var game = new BowlGame
            {
                TeamHome = "Ohio State",
                TeamAway = "Michigan",
                TeamHomeScore = null,
                TeamAwayScore = null,
                Status = GameStatus.Final
            };

            // Should treat null as 0, resulting in a tie
            game.WinningTeamName.Should().BeNull();
        }

        [Fact]
        public void IsFinal_ShouldReturnTrue_WhenStatusIsFinal()
        {
            var game = new BowlGame { Status = GameStatus.Final };
            game.IsFinal.Should().BeTrue();
        }

        [Fact]
        public void IsFinal_ShouldReturnFalse_WhenStatusIsScheduled()
        {
            var game = new BowlGame { Status = GameStatus.Scheduled };
            game.IsFinal.Should().BeFalse();
        }

        // --- IScorable contract defaults ---

        [Fact]
        public void PointValue_ShouldDefaultToOne()
        {
            var game = new BowlGame();
            game.PointValue.Should().Be(1);
        }

        [Fact]
        public void Round_ShouldDefaultToStandard()
        {
            var game = new BowlGame();
            game.Round.Should().Be(TournamentRound.Standard);
        }

        [Fact]
        public void BowlGame_ShouldImplementIScorable()
        {
            var game = new BowlGame
            {
                Id = "test-id",
                Status = GameStatus.Final,
                PointValue = 3,
                Round = TournamentRound.Championship,
                TeamHome = "Ohio State",
                TeamAway = "Notre Dame",
                TeamHomeScore = 28,
                TeamAwayScore = 14
            };

            IScorable scorable = game;

            scorable.Id.Should().Be("test-id");
            scorable.Status.Should().Be(GameStatus.Final);
            scorable.PointValue.Should().Be(3);
            scorable.Round.Should().Be(TournamentRound.Championship);
            scorable.TeamHome.Should().Be("Ohio State");
            scorable.TeamAway.Should().Be("Notre Dame");
            scorable.TeamHomeScore.Should().Be(28);
            scorable.TeamAwayScore.Should().Be(14);
            scorable.IsFinal.Should().BeTrue();
            scorable.WinningTeamName.Should().Be("Ohio State");
        }
    }
}
