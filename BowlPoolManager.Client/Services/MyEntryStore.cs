using BowlPoolManager.Core.Helpers;
using Microsoft.JSInterop;

namespace BowlPoolManager.Client.Services;

/// <summary>
/// Reads and writes "Find my entry" in this browser's own storage. Nothing leaves the device.
/// When storage is blocked (private browsing, a policy) reading gives an empty memory and saving does nothing,
/// so the page still works for the visit.
/// </summary>
public class MyEntryStore
{
    private readonly IJSRuntime _js;

    public MyEntryStore(IJSRuntime js) => _js = js;

    public async Task<MyEntryMemory> LoadAsync()
    {
        try
        {
            var stored = await _js.InvokeAsync<string?>("localStorage.getItem", MyEntryMemory.StorageKey);
            return MyEntryMemory.Parse(stored);
        }
        catch
        {
            return new MyEntryMemory();
        }
    }

    public async Task SaveAsync(MyEntryMemory memory)
    {
        try
        {
            if (memory.IsEmpty)
                await _js.InvokeVoidAsync("localStorage.removeItem", MyEntryMemory.StorageKey);
            else
                await _js.InvokeVoidAsync("localStorage.setItem", MyEntryMemory.StorageKey, memory.ToJson());
        }
        catch
        {
            // storage blocked: the choice still applies for this visit
        }
    }
}
