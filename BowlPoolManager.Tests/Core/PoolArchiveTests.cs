using Xunit;
using FluentAssertions;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Core
{
    public class PoolArchiveTests
    {
        #region PoolArchive Tests

        [Fact]
        public void PoolArchive_ShouldInitializeWithDefaults()
        {
            // Act
            var archive = new PoolArchive();

            // Assert
            archive.Id.Should().BeEmpty();
            archive.PoolId.Should().BeEmpty();
            archive.PoolName.Should().BeEmpty();
            archive.SeasonId.Should().BeEmpty();
            archive.Games.Should().NotBeNull();
            archive.Games.Should().BeEmpty();
            archive.Standings.Should().NotBeNull();
            archive.Standings.Should().BeEmpty();
            archive.Type.Should().Be("PoolArchive");
        }

        [Fact]
        public void PoolArchive_ArchivedOn_ShouldBeRecentUtc()
        {
            // Arrange
            var before = DateTime.UtcNow.AddSeconds(-1);

            // Act
            var archive = new PoolArchive();
            var after = DateTime.UtcNow.AddSeconds(1);

            // Assert
            archive.ArchivedOn.Should().BeOnOrAfter(before);
            archive.ArchivedOn.Should().BeOnOrBefore(after);
        }

        #endregion

        #region ArchiveGame Tests

        [Fact]
        public void ArchiveGame_WinningTeamName_ShouldReturnHomeTeam_WhenHomeScoreHigher()
        {
            // Arrange
            var game = new ArchiveGame
            {
                TeamHome = "Ohio State",
                TeamAway = "Michigan",
                TeamHomeScore = 42,
                TeamAwayScore = 27
            };

            // Act & Assert
            game.WinningTeamName.Should().Be("Ohio State");
        }

        [Fact]
        public void ArchiveGame_WinningTeamName_ShouldReturnAwayTeam_WhenAwayScoreHigher()
        {
            // Arrange
            var game = new ArchiveGame
            {
                TeamHome = "Ohio State",
                TeamAway = "Michigan",
                TeamHomeScore = 20,
                TeamAwayScore = 35
            };

            // Act & Assert
            game.WinningTeamName.Should().Be("Michigan");
        }

        [Fact]
        public void ArchiveGame_WinningTeamName_ShouldReturnTie_WhenScoresEqual()
        {
            // Arrange
            var game = new ArchiveGame
            {
                TeamHome = "Team A",
                TeamAway = "Team B",
                TeamHomeScore = 28,
                TeamAwayScore = 28
            };

            // Act & Assert
            game.WinningTeamName.Should().Be("Tie");
        }

        [Fact]
        public void ArchiveGame_WinningTeamName_ShouldReturnTBD_WhenHomeScoreNull()
        {
            // Arrange
            var game = new ArchiveGame
            {
                TeamHome = "Team A",
                TeamAway = "Team B",
                TeamHomeScore = null,
                TeamAwayScore = 28
            };

            // Act & Assert
            game.WinningTeamName.Should().Be("TBD");
        }

        [Fact]
        public void ArchiveGame_WinningTeamName_ShouldReturnTBD_WhenAwayScoreNull()
        {
            // Arrange
            var game = new ArchiveGame
            {
                TeamHome = "Team A",
                TeamAway = "Team B",
                TeamHomeScore = 28,
                TeamAwayScore = null
            };

            // Act & Assert
            game.WinningTeamName.Should().Be("TBD");
        }

        [Fact]
        public void ArchiveGame_WinningTeamName_ShouldReturnTBD_WhenBothScoresNull()
        {
            // Arrange
            var game = new ArchiveGame
            {
                TeamHome = "Team A",
                TeamAway = "Team B",
                TeamHomeScore = null,
                TeamAwayScore = null
            };

            // Act & Assert
            game.WinningTeamName.Should().Be("TBD");
        }

        [Fact]
        public void ArchiveGame_ShouldInitializeWithDefaults()
        {
            // Act
            var game = new ArchiveGame();

            // Assert
            game.GameId.Should().BeEmpty();
            game.BowlName.Should().BeEmpty();
            game.TeamHome.Should().BeEmpty();
            game.TeamAway.Should().BeEmpty();
            game.TeamHomeScore.Should().BeNull();
            game.TeamAwayScore.Should().BeNull();
            game.PointValue.Should().Be(0);
        }

        [Fact]
        public void ArchiveGame_BasketballFields_ShouldDefaultToNull()
        {
            // Act
            var game = new ArchiveGame();

            // Assert
            game.Round.Should().BeNull();
            game.Region.Should().BeNull();
            game.TeamHomeSeed.Should().BeNull();
            game.TeamAwaySeed.Should().BeNull();
        }

        [Fact]
        public void ArchiveGame_ShouldStoreBasketballFields_WhenSetExplicitly()
        {
            // Arrange & Act
            var game = new ArchiveGame
            {
                Round = TournamentRound.Sweet16,
                Region = "East",
                TeamHomeSeed = 1,
                TeamAwaySeed = 4
            };

            // Assert
            game.Round.Should().Be(TournamentRound.Sweet16);
            game.Region.Should().Be("East");
            game.TeamHomeSeed.Should().Be(1);
            game.TeamAwaySeed.Should().Be(4);
        }

        [Fact]
        public void ArchiveGame_Round_ShouldAcceptAllBasketballRounds()
        {
            // Verifies the Round field can hold each basketball TournamentRound value.
            var rounds = new[]
            {
                TournamentRound.FirstFour,
                TournamentRound.RoundOf64,
                TournamentRound.RoundOf32,
                TournamentRound.Sweet16,
                TournamentRound.Elite8,
                TournamentRound.FinalFour,
                TournamentRound.NationalChampionship
            };

            foreach (var round in rounds)
            {
                var game = new ArchiveGame { Round = round };
                game.Round.Should().Be(round);
            }
        }

        [Fact]
        public void ArchiveGame_BasketballFields_ShouldBeNullableAndIndependentOfScores()
        {
            // A football archive game (no basketball fields) should still compute WinningTeamName correctly.
            var game = new ArchiveGame
            {
                TeamHome = "Alabama",
                TeamAway = "Georgia",
                TeamHomeScore = 31,
                TeamAwayScore = 24,
                Round = null,
                Region = null,
                TeamHomeSeed = null,
                TeamAwaySeed = null
            };

            game.WinningTeamName.Should().Be("Alabama");
        }

        #endregion

        #region ArchiveStanding Tests

        [Fact]
        public void ArchiveStanding_ShouldInitializeWithDefaults()
        {
            // Act
            var standing = new ArchiveStanding();

            // Assert
            standing.PlayerName.Should().BeEmpty();
            standing.Rank.Should().Be(0);
            standing.TotalPoints.Should().Be(0);
            standing.CorrectPicks.Should().Be(0);
            standing.TieBreakerPoints.Should().Be(0);
            standing.TieBreakerDelta.Should().BeNull();
            standing.Picks.Should().NotBeNull();
            standing.Picks.Should().BeEmpty();
        }

        [Fact]
        public void ArchiveStanding_Picks_ShouldStorePicks()
        {
            // Arrange
            var standing = new ArchiveStanding();

            // Act
            standing.Picks["game1"] = "Team A";
            standing.Picks["game2"] = "Team B";

            // Assert
            standing.Picks.Should().HaveCount(2);
            standing.Picks["game1"].Should().Be("Team A");
        }

        #endregion
    }
}
