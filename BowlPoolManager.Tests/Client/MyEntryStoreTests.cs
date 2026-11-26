using BowlPoolManager.Client.Services;
using BowlPoolManager.Core.Helpers;
using FluentAssertions;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace BowlPoolManager.Tests.Client
{
    public class MyEntryStoreTests
    {
        [Fact]
        public async Task LoadAsync_ShouldReadTheStoredEntries()
        {
            var json = "{\"pools\":{\"p1\":{\"entryId\":\"e1\",\"name\":\"Sally\"}}}";
            var js = new Mock<IJSRuntime>();
            js.Setup(j => j.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object?[]?>())).ReturnsAsync(json);

            var memory = await new MyEntryStore(js.Object).LoadAsync();

            memory.Get("p1").Should().Be(new RememberedEntry("e1", "Sally"));
        }

        [Fact]
        public async Task LoadAsync_ShouldGiveAnEmptyMemory_WhenStorageIsBlocked()
        {
            var js = new Mock<IJSRuntime>();
            js.Setup(j => j.InvokeAsync<string?>(It.IsAny<string>(), It.IsAny<object?[]?>())).ThrowsAsync(new InvalidOperationException("blocked"));

            var memory = await new MyEntryStore(js.Object).LoadAsync();

            memory.IsEmpty.Should().BeTrue();
        }

        [Fact]
        public async Task SaveAsync_ShouldNotThrow_WhenStorageIsBlocked()
        {
            var js = new Mock<IJSRuntime>();
            js.Setup(j => j.InvokeAsync<IJSVoidResult>(It.IsAny<string>(), It.IsAny<object?[]?>())).ThrowsAsync(new InvalidOperationException("blocked"));
            var memory = new MyEntryMemory();
            memory.Remember("p1", "e1", "Sally");

            var act = () => new MyEntryStore(js.Object).SaveAsync(memory);

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task SaveAsync_ShouldRemoveTheKey_WhenNothingIsRemembered()
        {
            var js = new Mock<IJSRuntime>();
            js.Setup(j => j.InvokeAsync<IJSVoidResult>(It.IsAny<string>(), It.IsAny<object?[]?>())).ReturnsAsync((IJSVoidResult)null!);

            await new MyEntryStore(js.Object).SaveAsync(new MyEntryMemory());

            js.Verify(j => j.InvokeAsync<IJSVoidResult>("localStorage.removeItem", It.IsAny<object?[]?>()), Times.Once);
        }
    }
}
