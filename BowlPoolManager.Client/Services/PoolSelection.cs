using Microsoft.JSInterop;

namespace BowlPoolManager.Client.Services;

/// <summary>One pool in the switcher.</summary>
public record PoolChoice(string Id, string Name);

/// <summary>
/// The pools a page offers in the header switcher and which one is chosen (Release 2). A page calls
/// <see cref="Show"/> when it loads and <see cref="Clear"/> when it is left; the header and the page's own
/// switcher both read from here. The choice is remembered in the browser under the key the old pool
/// dropdowns already use, so pages that have not moved yet agree with it.
/// </summary>
public class PoolSelection
{
    public const string StorageKey = "lastSelectedPoolId";

    private readonly IJSRuntime _js;

    public PoolSelection(IJSRuntime js) => _js = js;

    public IReadOnlyList<PoolChoice> Pools { get; private set; } = Array.Empty<PoolChoice>();
    public string? CurrentId { get; private set; }

    /// <summary>True when there is more than one pool to switch between.</summary>
    public bool CanSwitch => Pools.Count > 1;

    /// <summary>Raised when the pools or the chosen pool change. Handlers must be quick and must not throw.</summary>
    public event Action? Changed;

    public void Show(IReadOnlyList<PoolChoice> pools, string? currentId)
    {
        Pools = pools;
        CurrentId = currentId;
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (Pools.Count == 0 && CurrentId == null) return;
        Pools = Array.Empty<PoolChoice>();
        CurrentId = null;
        Changed?.Invoke();
    }

    /// <summary>The visitor tapped a pool. Remembers it, then tells the page and the switchers.</summary>
    public async Task SelectAsync(string poolId)
    {
        if (poolId == CurrentId || Pools.All(p => p.Id != poolId)) return;
        CurrentId = poolId;
        try { await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, poolId); }
        catch { /* storage blocked: the choice still applies for this visit */ }
        Changed?.Invoke();
    }

    /// <summary>The pool this browser chose last, or null.</summary>
    public async Task<string?> GetSavedIdAsync()
    {
        try { return await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey); }
        catch { return null; }
    }
}
