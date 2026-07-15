using Microsoft.JSInterop;

namespace UniGroup.CRM.Client.Services;

/// <summary>
/// Manages the UI language ("en" / "ar"), persisted in localStorage and applied
/// to the document root (lang + dir attributes) via JS interop — mirroring
/// <see cref="ThemeService"/> so the choice survives reloads without a flash.
/// </summary>
public class LanguageService
{
    private readonly IJSRuntime _js;

    /// <summary>Initializes a new instance of the <see cref="LanguageService"/> class.</summary>
    public LanguageService(IJSRuntime js) => _js = js;

    /// <summary>Current language code ("en" or "ar").</summary>
    public string Current { get; private set; } = "en";

    /// <summary>Whether the current language renders right-to-left.</summary>
    public bool IsRtl => Current == "ar";

    /// <summary>Raised after the language changes so components can re-render.</summary>
    public event Action? OnChange;

    /// <summary>Reads the applied language from the document (set pre-paint by index.html).</summary>
    public async Task InitializeAsync()
    {
        Current = await _js.InvokeAsync<string>("unigroup.getLanguage");
        OnChange?.Invoke();
    }

    /// <summary>Sets the language, updates document lang/dir and notifies subscribers.</summary>
    public async Task SetLanguageAsync(string lang)
    {
        Current = lang == "ar" ? "ar" : "en";
        await _js.InvokeVoidAsync("unigroup.setLanguage", Current);
        OnChange?.Invoke();
    }

    /// <summary>Toggles between English and Arabic.</summary>
    public Task ToggleAsync() => SetLanguageAsync(Current == "ar" ? "en" : "ar");

    /// <summary>Translates a resource key using the current language.</summary>
    public string T(string key) => TranslationResources.Get(key, Current);
}
