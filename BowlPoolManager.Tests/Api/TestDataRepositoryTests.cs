using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using Newtonsoft.Json.Linq;
using BowlPoolManager.Api.Helpers;
using BowlPoolManager.Api.Repositories;

namespace BowlPoolManager.Tests.Api
{
    /// <summary>
    /// The repository is the second lock: even if a caller forgets the guard, it must refuse
    /// to touch Cosmos when the environment is not a test environment, and it must never
    /// delete from containers other than Seasons, Picks and PoolArchives.
    /// </summary>
    public class TestDataRepositoryTests
    {
        private static Mock<ITestEnvironment> Env(bool enabled)
        {
            var env = new Mock<ITestEnvironment>();
            env.Setup(e => e.IsEnabled).Returns(enabled);
            return env;
        }

        [Fact]
        public async Task DeleteAllAsync_ShouldThrowAndNotTouchCosmos_WhenNotTestEnvironment()
        {
            var client = new Mock<CosmosClient>();
            var sut = new TestDataRepository(client.Object, Env(false).Object);

            var act = () => sut.DeleteAllAsync("Seasons");

            await act.Should().ThrowAsync<InvalidOperationException>();
            client.Verify(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task UpsertAsync_ShouldThrowAndNotTouchCosmos_WhenNotTestEnvironment()
        {
            var client = new Mock<CosmosClient>();
            var sut = new TestDataRepository(client.Object, Env(false).Object);

            var act = () => sut.UpsertAsync("Seasons", "2025", new JObject { ["id"] = "x" });

            await act.Should().ThrowAsync<InvalidOperationException>();
            client.Verify(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData("Players")]
        [InlineData("Configuration")]
        [InlineData("MainContainer")]
        [InlineData("")]
        [InlineData("seasons")]
        public async Task DeleteAllAsync_ShouldThrowAndNotTouchCosmos_WhenContainerIsNotResettable(string container)
        {
            var client = new Mock<CosmosClient>();
            var sut = new TestDataRepository(client.Object, Env(true).Object);

            var act = () => sut.DeleteAllAsync(container);

            await act.Should().ThrowAsync<ArgumentException>();
            client.Verify(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData("Players")]
        [InlineData("MainContainer")]
        public async Task UpsertAsync_ShouldThrowAndNotTouchCosmos_WhenContainerIsNotWritable(string container)
        {
            var client = new Mock<CosmosClient>();
            var sut = new TestDataRepository(client.Object, Env(true).Object);

            var act = () => sut.UpsertAsync(container, "2025", new JObject { ["id"] = "x" });

            await act.Should().ThrowAsync<ArgumentException>();
            client.Verify(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        // ── Delete path: partition key choice ─────────────────────────────────────

        [Fact]
        public void PartitionKeyFor_ShouldDistinguishMissingNullAndValue_WhenDocumentHasSeasonId()
        {
            TestDataRepository.PartitionKeyFor(new JObject { ["id"] = "a", ["seasonId"] = "2026" })
                .Should().Be(new PartitionKey("2026"));
            TestDataRepository.PartitionKeyFor(new JObject { ["id"] = "b", ["seasonId"] = JValue.CreateNull() })
                .Should().Be(PartitionKey.Null);
            TestDataRepository.PartitionKeyFor(new JObject { ["id"] = "c" })
                .Should().Be(PartitionKey.None);
        }

        [Fact]
        public async Task DeleteAllAsync_ShouldDeleteEachDocumentWithItsOwnPartitionKey_WhenTestEnvironment()
        {
            var docs = new List<JObject>
            {
                new() { ["id"] = "a", ["seasonId"] = "2026" },
                new() { ["id"] = "b", ["seasonId"] = JValue.CreateNull() },
                new() { ["id"] = "c" }
            };

            var page = new Mock<FeedResponse<JObject>>();
            page.Setup(p => p.GetEnumerator()).Returns(() => docs.GetEnumerator());

            var iterator = new Mock<FeedIterator<JObject>>();
            iterator.SetupSequence(i => i.HasMoreResults).Returns(true).Returns(false);
            iterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(page.Object);

            var deletes = new List<(string id, PartitionKey key)>();
            var container = new Mock<Container>();
            container.Setup(c => c.GetItemQueryIterator<JObject>(It.IsAny<QueryDefinition>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>()))
                .Returns(iterator.Object);
            container.Setup(c => c.DeleteItemAsync<JObject>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .Callback<string, PartitionKey, ItemRequestOptions, CancellationToken>((id, key, _, _) => deletes.Add((id, key)))
                .ReturnsAsync(new Mock<ItemResponse<JObject>>().Object);

            var client = new Mock<CosmosClient>();
            client.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(container.Object);
            var sut = new TestDataRepository(client.Object, Env(true).Object);

            var count = await sut.DeleteAllAsync("Seasons");

            count.Should().Be(3);
            deletes.Should().BeEquivalentTo(new[]
            {
                ("a", new PartitionKey("2026")),
                ("b", PartitionKey.Null),
                ("c", PartitionKey.None)
            });
        }
    }
}
