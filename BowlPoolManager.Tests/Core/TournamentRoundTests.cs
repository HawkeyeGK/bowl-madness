using Xunit;
using FluentAssertions;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Core
{
    /// <summary>
    /// Verifies that TournamentRound integer values are stable.
    /// Football values must be backward-compatible with the deleted PlayoffRound enum
    /// so that Cosmos documents deserialized from the old schema continue to map correctly.
    /// Basketball values must match the declared specification.
    /// </summary>
    public class TournamentRoundTests
    {
        // --- Football / CFP rounds (backward-compatibility with PlayoffRound) ---

        [Fact]
        public void Standard_ShouldHaveValue_Zero()
        {
            ((int)TournamentRound.Standard).Should().Be(0);
        }

        [Fact]
        public void Round1_ShouldHaveValue_One()
        {
            ((int)TournamentRound.Round1).Should().Be(1);
        }

        [Fact]
        public void QuarterFinal_ShouldHaveValue_Two()
        {
            ((int)TournamentRound.QuarterFinal).Should().Be(2);
        }

        [Fact]
        public void SemiFinal_ShouldHaveValue_Three()
        {
            ((int)TournamentRound.SemiFinal).Should().Be(3);
        }

        [Fact]
        public void Championship_ShouldHaveValue_Four()
        {
            ((int)TournamentRound.Championship).Should().Be(4);
        }

        // --- Basketball / NCAA Tournament rounds ---

        [Fact]
        public void FirstFour_ShouldHaveValue_Ten()
        {
            ((int)TournamentRound.FirstFour).Should().Be(10);
        }

        [Fact]
        public void RoundOf64_ShouldHaveValue_Eleven()
        {
            ((int)TournamentRound.RoundOf64).Should().Be(11);
        }

        [Fact]
        public void RoundOf32_ShouldHaveValue_Twelve()
        {
            ((int)TournamentRound.RoundOf32).Should().Be(12);
        }

        [Fact]
        public void Sweet16_ShouldHaveValue_Thirteen()
        {
            ((int)TournamentRound.Sweet16).Should().Be(13);
        }

        [Fact]
        public void Elite8_ShouldHaveValue_Fourteen()
        {
            ((int)TournamentRound.Elite8).Should().Be(14);
        }

        [Fact]
        public void FinalFour_ShouldHaveValue_Fifteen()
        {
            ((int)TournamentRound.FinalFour).Should().Be(15);
        }

        [Fact]
        public void NationalChampionship_ShouldHaveValue_Sixteen()
        {
            ((int)TournamentRound.NationalChampionship).Should().Be(16);
        }

        // --- No overlap between football and basketball namespaces ---

        [Fact]
        public void FootballAndBasketballValues_ShouldNotOverlap()
        {
            var footballValues = new[]
            {
                (int)TournamentRound.Standard,
                (int)TournamentRound.Round1,
                (int)TournamentRound.QuarterFinal,
                (int)TournamentRound.SemiFinal,
                (int)TournamentRound.Championship
            };

            var basketballValues = new[]
            {
                (int)TournamentRound.FirstFour,
                (int)TournamentRound.RoundOf64,
                (int)TournamentRound.RoundOf32,
                (int)TournamentRound.Sweet16,
                (int)TournamentRound.Elite8,
                (int)TournamentRound.FinalFour,
                (int)TournamentRound.NationalChampionship
            };

            footballValues.Should().NotIntersectWith(basketballValues);
        }
    }
}
