using BowlPoolManager.Client.Services;
using FluentAssertions;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace BowlPoolManager.Tests.Client
{
    public class PoolSelectionTests
    {
        private static readonly PoolChoice[] Pools = { new("a", "Family Pool"), new("b", "Office Pool") };

        private static (PoolSelection sel, Mock<IJSRuntime> js) Create(bool storageThrows = false)
        {
            var js = new Mock<IJSRuntime>();
            var setup = js.Setup(j => j.InvokeAsync<IJSVoidResult>(It.IsAny<string>(), It.IsAny<object?[]?>()));
            if (storageThrows) setup.ThrowsAsync(new InvalidOperationException("storage blocked"));
            else setup.ReturnsAsync((IJSVoidResult)null!);
            return (new PoolSelection(js.Object), js);
        }

        [Fact]
        public void Show_ShouldRaiseChangedAndAllowSwitching_WhenThereAreTwoPools()
        {
            var (sel, _) = Create();
            var raised = 0;
            sel.Changed += () => raised++;

            sel.Show(Pools, "a");

            raised.Should().Be(1);
            sel.CurrentId.Should().Be("a");
            sel.CanSwitch.Should().BeTrue();
        }

        [Fact]
        public void CanSwitch_ShouldBeFalse_WhenThereIsOnePool()
        {
            var (sel, _) = Create();
            sel.Show(new[] { Pools[0] }, "a");

            sel.CanSwitch.Should().BeFalse();
        }

        [Fact]
        public async Task SelectAsync_ShouldChangeTheChoiceAndRememberIt_WhenThePoolIsKnown()
        {
            var (sel, js) = Create();
            sel.Show(Pools, "a");
            var raised = 0;
            sel.Changed += () => raised++;

            await sel.SelectAsync("b");

            sel.CurrentId.Should().Be("b");
            raised.Should().Be(1);
            js.Verify(j => j.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object?[]?>()), Times.Once);
        }

        [Theory]
        [InlineData("a")]
        [InlineData("unknown")]
        public async Task SelectAsync_ShouldDoNothing_WhenThePoolIsCurrentOrUnknown(string id)
        {
            var (sel, js) = Create();
            sel.Show(Pools, "a");
            var raised = 0;
            sel.Changed += () => raised++;

            await sel.SelectAsync(id);

            sel.CurrentId.Should().Be("a");
            raised.Should().Be(0);
            js.Verify(j => j.InvokeAsync<IJSVoidResult>(It.IsAny<string>(), It.IsAny<object?[]?>()), Times.Never);
        }

        [Fact]
        public async Task SelectAsync_ShouldStillApplyTheChoice_WhenBrowserStorageIsBlocked()
        {
            var (sel, _) = Create(storageThrows: true);
            sel.Show(Pools, "a");

            await sel.SelectAsync("b");

            sel.CurrentId.Should().Be("b");
        }

        [Fact]
        public void Clear_ShouldEmptyThePoolsAndRaiseChanged_OnlyWhenSomethingWasShown()
        {
            var (sel, _) = Create();
            var raised = 0;
            sel.Changed += () => raised++;

            sel.Clear();
            raised.Should().Be(0);

            sel.Show(Pools, "a");
            sel.Clear();

            raised.Should().Be(2);
            sel.Pools.Should().BeEmpty();
            sel.CurrentId.Should().BeNull();
        }

        [Fact]
        public async Task GetSavedIdAsync_ShouldReturnNull_WhenBrowserStorageIsBlocked()
        {
            var js = new Mock<IJSRuntime>();
            js.Setup(j => j.InvokeAsync<string?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
              .ThrowsAsync(new InvalidOperationException());

            (await new PoolSelection(js.Object).GetSavedIdAsync()).Should().BeNull();
        }
    }
}
