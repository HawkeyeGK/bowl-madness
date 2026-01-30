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
