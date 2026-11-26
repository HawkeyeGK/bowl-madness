using FluentAssertions;
using BowlPoolManager.Core.Helpers;

namespace BowlPoolManager.Tests.Core
{
    public class MyEntryMemoryTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not json at all")]
        [InlineData("[1,2,3]")]
        [InlineData("{\"pools\": 5}")]
        [InlineData("{\"pools\": {\"p1\": 7}}")]
        public void Parse_ShouldGiveAnEmptyMemory_WhenStoredTextIsMissingOrDamaged(string? stored)
        {
            var memory = MyEntryMemory.Parse(stored);

            memory.IsEmpty.Should().BeTrue();
            memory.Get("p1").Should().BeNull();
        }

        [Fact]
        public void Remember_ShouldKeepOneEntryPerPool_WhenTheSamePoolIsRememberedTwice()
        {
            var memory = new MyEntryMemory();
            memory.Remember("p1", "e1", "Sally");
            memory.Remember("p1", "e2", "Sally 2");

            memory.Get("p1").Should().Be(new RememberedEntry("e2", "Sally 2"));
        }

        [Fact]
        public void Remember_ShouldKeepPoolsApart_WhenTwoPoolsAreRemembered()
        {
            var memory = new MyEntryMemory();
            memory.Remember("p1", "e1", "Sally");
            memory.Remember("p2", "e9", "Sal");

            memory.Get("p1")!.EntryId.Should().Be("e1");
            memory.Get("p2")!.EntryId.Should().Be("e9");
        }

        [Fact]
        public void Forget_ShouldRemoveOnlyThatPool()
        {
            var memory = new MyEntryMemory();
            memory.Remember("p1", "e1", "Sally");
            memory.Remember("p2", "e9", "Sal");

            memory.Forget("p1");

            memory.Get("p1").Should().BeNull();
            memory.Get("p2").Should().NotBeNull();
        }

        [Fact]
        public void Forget_ShouldDoNothing_WhenThePoolWasNeverRemembered()
        {
            var memory = new MyEntryMemory();

            var act = () => memory.Forget("nope");

            act.Should().NotThrow();
        }

        [Fact]
        public void ToJson_ShouldRoundTrip_WhenParsedAgain()
        {
            var memory = new MyEntryMemory();
            memory.Remember("p1", "e1", "Grandma \"Jo\" & Co");

            var again = MyEntryMemory.Parse(memory.ToJson());

            again.Get("p1").Should().Be(new RememberedEntry("e1", "Grandma \"Jo\" & Co"));
        }

        [Fact]
        public void Parse_ShouldSkipRecordsWithoutAnEntryId_WhenOtherRecordsAreFine()
        {
            var memory = MyEntryMemory.Parse("{\"pools\":{\"p1\":{\"entryId\":\"\",\"name\":\"x\"},\"p2\":{\"entryId\":\"e2\",\"name\":\"Nora\"}}}");

            memory.Get("p1").Should().BeNull();
            memory.Get("p2")!.Name.Should().Be("Nora");
        }

        [Fact]
        public void Remember_ShouldIgnoreBlankIds()
        {
            var memory = new MyEntryMemory();

            memory.Remember("", "e1", "x");
            memory.Remember("p1", "", "x");

            memory.IsEmpty.Should().BeTrue();
        }
    }
}
