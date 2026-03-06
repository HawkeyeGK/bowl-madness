using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using BowlPoolManager.Client.Services;
using BowlPoolManager.Core.Domain;

namespace BowlPoolManager.Tests.Client
{
    /// <summary>
    /// Minimal NavigationManager stub. NavigationManager is abstract; Moq cannot mock it
    /// directly. We subclass it and call Initialize() to set the URI, then implement the
    /// required abstract core method.
    /// </summary>
    internal sealed class StubNavigationManager : NavigationManager
    {
        public StubNavigationManager(string uri)
        {
            Initialize(uri, uri);
        }

        protected override void NavigateToCore(string uri, NavigationOptions options) { }
    }

    public class SiteContextTests
    {
        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static SiteContext Build(
            string uri,
            string environment,
            Mock<IJSRuntime>? jsMock = null)
        {
            var nav = new StubNavigationManager(uri);
            var envMock = new Mock<IWebAssemblyHostEnvironment>();
            envMock.Setup(e => e.Environment).Returns(environment);
            envMock.Setup(e => e.BaseAddress).Returns(uri);
            var js = jsMock ?? new Mock<IJSRuntime>();
            return new SiteContext(nav, envMock.Object, js.Object);
        }

        private static Mock<IJSRuntime> JsWithStoredValue(string? storedValue)
        {
            var jsMock = new Mock<IJSRuntime>();
            jsMock
                .Setup(j => j.InvokeAsync<string?>(
                    "localStorage.getItem",
                    It.IsAny<object?[]>()))
                .ReturnsAsync(storedValue);
            return jsMock;
        }

        // -----------------------------------------------------------------------
        // ActiveSport — hostname detection
        // -----------------------------------------------------------------------

        [Fact]
        public void ActiveSport_ShouldReturnBasketball_WhenHostContainsHoopsMadness()
        {
            var sut = Build("https://hoops-madness.com/", "Production");

            sut.ActiveSport.Should().Be(Sport.Basketball);
        }

        [Fact]
        public void ActiveSport_ShouldReturnFootball_WhenHostContainsBowlMadness()
        {
            var sut = Build("https://bowl-madness.com/", "Production");

            sut.ActiveSport.Should().Be(Sport.Football);
        }

        [Fact]
        public void ActiveSport_ShouldReturnBasketball_WhenHostContainsWwwHoopsMadness()
        {
            // Ensures subdomain variant is covered by the Contains check.
            var sut = Build("https://www.hoops-madness.com/standings", "Production");

            sut.ActiveSport.Should().Be(Sport.Basketball);
        }

        // -----------------------------------------------------------------------
        // ActiveSport — localhost path-prefix fallback
        // -----------------------------------------------------------------------

        [Fact]
        public void ActiveSport_ShouldReturnBasketball_WhenLocalhostPathStartsWithBasketball()
        {
            var sut = Build("https://localhost:5000/basketball/picks", "Development");

            sut.ActiveSport.Should().Be(Sport.Basketball);
        }

        [Fact]
        public void ActiveSport_ShouldReturnFootball_WhenLocalhostPathHasNoBasketballPrefix()
        {
            var sut = Build("https://localhost:5000/standings", "Development");

            sut.ActiveSport.Should().Be(Sport.Football);
        }

        [Fact]
        public void ActiveSport_ShouldReturnFootball_WhenLocalhostAtRoot()
        {
            var sut = Build("https://localhost:5000/", "Development");

            sut.ActiveSport.Should().Be(Sport.Football);
        }

        // -----------------------------------------------------------------------
        // ActiveSport — host detection is case-insensitive
        // -----------------------------------------------------------------------

        [Fact]
        public void ActiveSport_ShouldReturnBasketball_WhenHostNameIsMixedCase()
        {
            // NavigationManager normalises the URI, but we test that our ToLowerInvariant
            // call handles any remaining case differences.
            var sut = Build("https://Hoops-Madness.com/", "Production");

            sut.ActiveSport.Should().Be(Sport.Basketball);
        }

        // -----------------------------------------------------------------------
        // IsDevMode
        // -----------------------------------------------------------------------

        [Fact]
        public void IsDevMode_ShouldReturnTrue_WhenEnvironmentIsDevelopment()
        {
            var sut = Build("https://localhost:5000/", "Development");

            sut.IsDevMode.Should().BeTrue();
        }

        [Fact]
        public void IsDevMode_ShouldReturnFalse_WhenEnvironmentIsProduction()
        {
            var sut = Build("https://bowl-madness.com/", "Production");

            sut.IsDevMode.Should().BeFalse();
        }

        // -----------------------------------------------------------------------
        // InitializeAsync — dev override from localStorage
        // -----------------------------------------------------------------------

        [Fact]
        public async Task InitializeAsync_ShouldSetDevOverrideToBasketball_WhenStoredValueIsBasketball()
        {
            var js = JsWithStoredValue("Basketball");
            var sut = Build("https://localhost:5000/", "Development", js);

            await sut.InitializeAsync();

            sut.ActiveSport.Should().Be(Sport.Basketball);
        }

        [Fact]
        public async Task InitializeAsync_ShouldSetDevOverrideToFootball_WhenStoredValueIsFootball()
        {
            var js = JsWithStoredValue("Football");
            var sut = Build("https://localhost:5000/basketball", "Development", js);

            // Even though the path implies Basketball, the stored override wins.
            await sut.InitializeAsync();

            sut.ActiveSport.Should().Be(Sport.Football);
        }

        [Fact]
        public async Task InitializeAsync_ShouldLeaveOverrideNull_WhenStoredValueIsNull()
        {
            var js = JsWithStoredValue(null);
            var sut = Build("https://localhost:5000/", "Development", js);

            await sut.InitializeAsync();

            // No override — hostname/path fallback applies; root localhost → Football.
            sut.ActiveSport.Should().Be(Sport.Football);
        }

        [Fact]
        public async Task InitializeAsync_ShouldLeaveOverrideNull_WhenStoredValueIsInvalid()
        {
            var js = JsWithStoredValue("rugby");
            var sut = Build("https://localhost:5000/basketball", "Development", js);

            await sut.InitializeAsync();

            // Enum.TryParse fails → _devOverride stays null → path prefix applies → Basketball.
            sut.ActiveSport.Should().Be(Sport.Basketball);
        }

        [Fact]
        public async Task InitializeAsync_ShouldNotReadLocalStorage_WhenEnvironmentIsProduction()
        {
            var jsMock = new Mock<IJSRuntime>();
            var sut = Build("https://bowl-madness.com/", "Production", jsMock);

            await sut.InitializeAsync();

            jsMock.Verify(
                j => j.InvokeAsync<string?>(It.IsAny<string>(), It.IsAny<object?[]>()),
                Times.Never);
        }

        [Fact]
        public async Task InitializeAsync_ShouldOnlyReadLocalStorageOnce_WhenCalledMultipleTimes()
        {
            var js = JsWithStoredValue("Basketball");
            var sut = Build("https://localhost:5000/", "Development", js);

            await sut.InitializeAsync();
            await sut.InitializeAsync();
            await sut.InitializeAsync();

            js.Verify(
                j => j.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object?[]>()),
                Times.Once);
        }

        [Fact]
        public async Task InitializeAsync_ShouldNotThrow_WhenLocalStorageThrows()
        {
            var jsMock = new Mock<IJSRuntime>();
            jsMock
                .Setup(j => j.InvokeAsync<string?>(It.IsAny<string>(), It.IsAny<object?[]>()))
                .ThrowsAsync(new InvalidOperationException("localStorage unavailable"));
            var sut = Build("https://localhost:5000/", "Development", jsMock);

            var act = () => sut.InitializeAsync();

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task InitializeAsync_ShouldFallBackToPathDetection_WhenLocalStorageThrows()
        {
            var jsMock = new Mock<IJSRuntime>();
            jsMock
                .Setup(j => j.InvokeAsync<string?>(It.IsAny<string>(), It.IsAny<object?[]>()))
                .ThrowsAsync(new InvalidOperationException("localStorage unavailable"));
            var sut = Build("https://localhost:5000/basketball", "Development", jsMock);

            await sut.InitializeAsync();

            sut.ActiveSport.Should().Be(Sport.Basketball);
        }

        // -----------------------------------------------------------------------
        // SetSportAsync — dev override write
        // -----------------------------------------------------------------------

        [Fact]
        public async Task SetSportAsync_ShouldUpdateActiveSport_Immediately()
        {
            var jsMock = new Mock<IJSRuntime>();
            jsMock
                .Setup(j => j.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    "localStorage.setItem",
                    It.IsAny<object?[]>()))
                .ReturnsAsync(Mock.Of<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());
            var sut = Build("https://localhost:5000/", "Development", jsMock);

            await sut.SetSportAsync(Sport.Basketball);

            sut.ActiveSport.Should().Be(Sport.Basketball);
        }

        [Fact]
        public async Task SetSportAsync_ShouldPersistToLocalStorage_WithCorrectKey()
        {
            var jsMock = new Mock<IJSRuntime>();
            jsMock
                .Setup(j => j.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    "localStorage.setItem",
                    It.IsAny<object?[]>()))
                .ReturnsAsync(Mock.Of<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());
            var sut = Build("https://localhost:5000/", "Development", jsMock);

            await sut.SetSportAsync(Sport.Football);

            jsMock.Verify(
                j => j.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    "localStorage.setItem",
                    It.Is<object?[]>(args =>
                        args.Length == 2 &&
                        (string)args[0]! == "devSportOverride" &&
                        (string)args[1]! == "Football")),
                Times.Once);
        }

        [Fact]
        public async Task SetSportAsync_ShouldFireOnChange_AfterSettingSport()
        {
            var jsMock = new Mock<IJSRuntime>();
            jsMock
                .Setup(j => j.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>()))
                .ReturnsAsync(Mock.Of<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());
            var sut = Build("https://localhost:5000/", "Development", jsMock);

            var eventFired = false;
            sut.OnChange += () => eventFired = true;

            await sut.SetSportAsync(Sport.Basketball);

            eventFired.Should().BeTrue();
        }

        [Fact]
        public async Task SetSportAsync_ShouldNotThrow_WhenLocalStorageThrows()
        {
            var jsMock = new Mock<IJSRuntime>();
            jsMock
                .Setup(j => j.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                    It.IsAny<string>(),
                    It.IsAny<object?[]>()))
                .ThrowsAsync(new InvalidOperationException("localStorage unavailable"));
            var sut = Build("https://localhost:5000/", "Development", jsMock);

            var act = () => sut.SetSportAsync(Sport.Basketball);

            await act.Should().NotThrowAsync();
            // Despite the storage failure the in-memory override is still set.
            sut.ActiveSport.Should().Be(Sport.Basketball);
        }

        // -----------------------------------------------------------------------
        // ActiveSport — dev override takes precedence over hostname
        // -----------------------------------------------------------------------

        [Fact]
        public async Task ActiveSport_ShouldReturnOverride_WhenDevOverrideSetAndHostnameDisagrees()
        {
            // Hostname says Football; dev override should win.
            var js = JsWithStoredValue("Basketball");
            var sut = Build("https://localhost:5000/", "Development", js);

            await sut.InitializeAsync();

            sut.ActiveSport.Should().Be(Sport.Basketball);
        }
    }
}
