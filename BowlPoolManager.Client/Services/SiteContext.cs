using BowlPoolManager.Core.Domain;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

namespace BowlPoolManager.Client.Services;

public class SiteContext : ISiteContext
{
    private const string DevOverrideKey = "devSportOverride";

    private readonly NavigationManager _nav;
    private readonly IWebAssemblyHostEnvironment _env;
    private readonly IJSRuntime _js;

    private Sport? _devOverride;
    private bool _initialized;

    public event Action? OnChange;

    public SiteContext(NavigationManager nav, IWebAssemblyHostEnvironment env, IJSRuntime js)
    {
        _nav = nav;
        _env = env;
        _js = js;
    }

    public bool IsDevMode => _env.IsDevelopment();

    public Sport ActiveSport
    {
        get
        {
            if (_devOverride.HasValue)
                return _devOverride.Value;

            var uri = new Uri(_nav.Uri);
            var host = uri.Host.ToLowerInvariant();

            if (host.Contains("hoops-madness"))
                return Sport.Basketball;
            if (host.Contains("bowl-madness"))
                return Sport.Football;

            // Localhost fallback: path prefix
            var path = uri.AbsolutePath.ToLowerInvariant();
            if (path.StartsWith("/basketball"))
                return Sport.Basketball;

            return Sport.Football;
        }
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        _initialized = true;

        if (!IsDevMode)
            return;

        try
        {
            var stored = await _js.InvokeAsync<string?>("localStorage.getItem", DevOverrideKey);
            if (Enum.TryParse<Sport>(stored, out var sport))
                _devOverride = sport;
        }
        catch
        {
            // localStorage unavailable; leave _devOverride null so hostname detection applies
        }
    }

    public async Task SetSportAsync(Sport sport)
    {
        if (!IsDevMode)
            return;

        _devOverride = sport;
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", DevOverrideKey, sport.ToString());
        }
        catch { /* ignore */ }
        OnChange?.Invoke();
    }
}
