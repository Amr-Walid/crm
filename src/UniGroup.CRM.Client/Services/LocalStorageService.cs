using System.Text.Json;
using Microsoft.JSInterop;

namespace UniGroup.CRM.Client.Services;

/// <summary>
/// Minimal typed wrapper over browser localStorage via JS interop.
/// </summary>
public class LocalStorageService
{
    private readonly IJSRuntime _js;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Initializes a new instance of the <see cref="LocalStorageService"/> class.</summary>
    public LocalStorageService(IJSRuntime js) => _js = js;

    /// <summary>Gets and deserializes an item, or default when missing/corrupt.</summary>
    public async Task<T?> GetItemAsync<T>(string key)
    {
        try
        {
            var raw = await _js.InvokeAsync<string?>("localStorage.getItem", key);
            return raw is null ? default : JsonSerializer.Deserialize<T>(raw, JsonOptions);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>Serializes and stores an item.</summary>
    public async Task SetItemAsync<T>(string key, T value) =>
        await _js.InvokeVoidAsync("localStorage.setItem", key, JsonSerializer.Serialize(value, JsonOptions));

    /// <summary>Removes an item.</summary>
    public async Task RemoveItemAsync(string key) =>
        await _js.InvokeVoidAsync("localStorage.removeItem", key);
}
