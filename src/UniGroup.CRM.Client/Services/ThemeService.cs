using Microsoft.JSInterop;

namespace UniGroup.CRM.Client.Services;

/// <summary>
/// Manages the light/dark theme, persisted in localStorage and applied
/// to the document root via JS interop (before first paint on reload).
/// </summary>
public class ThemeService
{
    private readonly IJSRuntime _js;

    /// <summary>Initializes a new instance of the <see cref="ThemeService"/> class.</summary>
    public ThemeService(IJSRuntime js) => _js = js;

    /// <summary>Current theme name ("light" or "dark").</summary>
    public string Current { get; private set; } = "light";

    /// <summary>Raised after the theme changes.</summary>
    public event Action? OnChange;

    /// <summary>Reads the applied theme from the document.</summary>
    public async Task InitializeAsync()
    {
        Current = await _js.InvokeAsync<string>("unigroup.getTheme");
        OnChange?.Invoke();
    }

    /// <summary>Toggles between light and dark themes.</summary>
    public async Task ToggleAsync()
    {
        Current = Current == "dark" ? "light" : "dark";
        await _js.InvokeVoidAsync("unigroup.setTheme", Current);
        OnChange?.Invoke();
    }
}
