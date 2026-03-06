using Xunit;
using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core;

namespace BowlPoolManager.Tests.Core
{
    public class HoopsGameTests
    {
        // --- IsFinal ---

        [Fact]
        public void IsFinal_ShouldReturnTrue_WhenStatusIsFinal()
        {
            var game = new HoopsGame { Status = GameStatus.Final };
            game.IsFinal.Should().BeTrue();
        }

        [Fact]
        public void IsFinal_ShouldReturnFalse_WhenStatusIsScheduled()
        {
            var game = new HoopsGame { Status = GameStatus.Scheduled };
            game.IsFinal.Should().BeFalse();
        }

        [Fact]
        public void IsFinal_ShouldReturnFalse_WhenStatusIsInProgress()
        {
            var game = new HoopsGame { Status = GameStatus.InProgress };
            game.IsFinal.Should().BeFalse();
        }

        // --- WinningTeamName ---

        [Fact]
        public void WinningTeamName_ShouldReturnHomeTeam_WhenHomeScoreIsHigher()
        {
            var game = new HoopsGame
            {
                TeamHome = "Duke",
                TeamAway = "UNC",
                TeamHomeScore = 78,
                TeamAwayScore = 72,
                Status = GameStatus.Final
            };

            game.WinningTeamName.Should().Be("Duke");
        }

        [Fact]
        public void WinningTeamName_ShouldReturnAwayTeam_WhenAwayScoreIsHigher()
        {
            var game = new HoopsGame
            {
                TeamHome = "Duke",
                TeamAway = "UNC",
                TeamHomeScore = 65,
                TeamAwayScore = 80,
                Status = GameStatus.Final
            };

            game.WinningTeamName.Should().Be("UNC");
        }

        [Fact]
        public void WinningTeamName_ShouldReturnNull_WhenGameIsNotFinal()
        {
            var game = new HoopsGame
            {
                TeamHome = "Duke",
                TeamAway = "UNC",
                TeamHomeScore = 40,
                TeamAwayScore = 38,
                Status = GameStatus.InProgress
            };

            game.WinningTeamName.Should().BeNull();
        }

        [Fact]
        public void WinningTeamName_ShouldReturnNull_WhenScoresAreTied()
        {
            var game = new HoopsGame
            {
                TeamHome = "Duke",
                TeamAway = "UNC",
                TeamHomeScore = 70,
                TeamAwayScore = 70,
                Status = GameStatus.Final
            };

            game.WinningTeamName.Should().BeNull();
        }

        [Fact]
        public void WinningTeamName_ShouldHandleNullScores()
        {
            // Null scores are treated as 0 — results in a tie → null
            var game = new HoopsGame
            {
                TeamHome = "Duke",
                TeamAway = "UNC",
                TeamHomeScore = null,
                TeamAwayScore = null,
                Status = GameStatus.Final
            };

            game.WinningTeamName.Should().BeNull();
        }

        // --- LosingTeamName ---

        [Fact]
        public void LosingTeamName_ShouldReturnAwayTeam_WhenHomeScoreIsHigher()
        {
            var game = new HoopsGame
            {
                TeamHome = "Duke",
                TeamAway = "UNC",
                TeamHomeScore = 78,
                TeamAwayScore = 72,
                Status = GameStatus.Final
            };

            game.LosingTeamName.Should().Be("UNC");
        }

        [Fact]
        public void LosingTeamName_ShouldReturnHomeTeam_WhenAwayScoreIsHigher()
        {
            var game = new HoopsGame
            {
                TeamHome = "Duke",
                TeamAway = "UNC",
                TeamHomeScore = 60,
                TeamAwayScore = 75,
                Status = GameStatus.Final
            };

            game.LosingTeamName.Should().Be("Duke");
        }

        [Fact]
        public void LosingTeamName_ShouldReturnNull_WhenGameIsNotFinal()
        {
            var game = new HoopsGame
            {
                TeamHome = "Duke",
                TeamAway = "UNC",
                TeamHomeScore = 40,
                TeamAwayScore = 38,
                Status = GameStatus.Scheduled
            };

            game.LosingTeamName.Should().BeNull();
        }

        [Fact]
        public void LosingTeamName_ShouldReturnNull_WhenScoresAreTied()
        {
            var game = new HoopsGame
            {
                TeamHome = "Duke",
                TeamAway = "UNC",
                TeamHomeScore = 55,
                TeamAwayScore = 55,
                Status = GameStatus.Final
            };

            game.LosingTeamName.Should().BeNull();
        }

        // --- Default values / IScorable contract ---

        [Fact]
        public void PointValue_ShouldDefaultToZero()
        {
            var game = new HoopsGame();
            game.PointValue.Should().Be(0);
        }

        [Fact]
        public void Round_ShouldDefaultToRoundOf64()
        {
            var game = new HoopsGame();
            game.Round.Should().Be(TournamentRound.RoundOf64);
        }

        [Fact]
        public void Status_ShouldDefaultToScheduled()
        {
            var game = new HoopsGame();
            game.Status.Should().Be(GameStatus.Scheduled);
        }

        [Fact]
        public void Type_ShouldMatchHoopsGameDocumentTypeConstant()
        {
            var game = new HoopsGame();
            game.Type.Should().Be(Constants.DocumentTypes.HoopsGame);
        }

        [Fact]
        public void HoopsGame_ShouldImplementIScorable()
        {
            // Verifies the interface contract is satisfied at compile time and at runtime.
            var game = new HoopsGame
            {
                Id = "test-id",
                Status = GameStatus.Final,
                PointValue = 5,
                Round = TournamentRound.Sweet16,
                TeamHome = "Duke",
                TeamAway = "UNC",
                TeamHomeScore = 80,
                TeamAwayScore = 70
            };

            IScorable scorable = game;

            scorable.Id.Should().Be("test-id");
            scorable.Status.Should().Be(GameStatus.Final);
            scorable.PointValue.Should().Be(5);
            scorable.Round.Should().Be(TournamentRound.Sweet16);
            scorable.TeamHome.Should().Be("Duke");
            scorable.TeamAway.Should().Be("UNC");
            scorable.TeamHomeScore.Should().Be(80);
            scorable.TeamAwayScore.Should().Be(70);
            scorable.IsFinal.Should().BeTrue();
            scorable.WinningTeamName.Should().Be("Duke");
        }
    }
}
