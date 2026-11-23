using FluentAssertions;
using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    public class BracketSlotsTests
    {
        private static BowlGame Game(string id, string home, string away, int hour, string? next = null) => new()
        {
            Id = id, BowlName = id, TeamHome = home, TeamAway = away, NextGameId = next,
            StartTime = new DateTime(2026, 12, 20, hour, 0, 0, DateTimeKind.Utc)
        };

        [Fact]
        public void IsHomeSlot_ShouldPutTheEarlierKickoffAtHome_WhenTwoGamesFeedTheGame()
        {
            var early = Game("early", "A", "B", 12, "next");
            var late = Game("late", "C", "D", 18, "next");
            var next = Game("next", "Winner of early", "Winner of late", 23);
            var all = new[] { late, early, next }; // listed out of order on purpose

            BracketSlots.IsHomeSlot(early, next, all).Should().BeTrue();
            BracketSlots.IsHomeSlot(late, next, all).Should().BeFalse();
        }

        [Fact]
        public void IsHomeSlot_ShouldUseTheEmptySlot_WhenOneFeederAndTheOtherSlotHoldsASeed()
        {
            var feeder = Game("r1", "Miami", "Texas A&M", 12, "qf");
            var seedHome = Game("qf", "Ohio State", "Winner of r1", 18);
            var seedAway = Game("qf2", "Winner of r1", "Ohio State", 18);
            feeder.NextGameId = "qf";

            BracketSlots.IsHomeSlot(feeder, seedHome, new[] { feeder, seedHome }).Should().BeFalse();
            seedAway.Id = "qf";
            BracketSlots.IsHomeSlot(feeder, seedAway, new[] { feeder, seedAway }).Should().BeTrue();
        }

        [Fact]
        public void IsHomeSlot_ShouldKeepTheFeedersSlot_WhenItStillHoldsAPreviousWinner()
        {
            var feeder = Game("r1", "Miami", "Texas A&M", 12, "qf");
            var next = Game("qf", "Ohio State", "Miami", 18); // Miami was copied in; the seed is at home

            BracketSlots.IsHomeSlot(feeder, next, new[] { feeder, next }).Should().BeFalse();
        }

        [Fact]
        public void IsHomeSlot_ShouldDefaultToHome_WhenBothSlotsAreEmpty()
        {
            var feeder = Game("r1", "Miami", "Texas A&M", 12, "qf");
            var next = Game("qf", "TBD", "TBD", 18);

            BracketSlots.IsHomeSlot(feeder, next, new[] { feeder, next }).Should().BeTrue();
        }

        [Fact]
        public void IsHomeSlot_ShouldBreakStartTimeTiesById_WhenFeedersKickOffTogether()
        {
            var a = Game("a", "A", "B", 12, "next");
            var b = Game("b", "C", "D", 12, "next");
            var next = Game("next", "TBD", "TBD", 23);

            BracketSlots.IsHomeSlot(a, next, new[] { b, a, next }).Should().BeTrue();
            BracketSlots.IsHomeSlot(b, next, new[] { b, a, next }).Should().BeFalse();
        }

        [Theory]
        [InlineData(0, 2, "TBD", "TBD", true)]
        [InlineData(1, 2, "TBD", "TBD", false)]
        [InlineData(0, 1, "Seed", "Old Winner", false)]
        [InlineData(0, 1, "Old Winner", "Seed", true)]
        [InlineData(-1, 1, "Seed", "TBD", true)]
        public void IsHomeSlot_ShouldFollowTheSameRule_WhenGivenNamesOnly(int index, int count, string home, string away, bool expected)
        {
            BracketSlots.IsHomeSlot(index, count, home, away, "Old Winner", "Other Team", "Winner of X").Should().Be(expected);
        }
    }
}
