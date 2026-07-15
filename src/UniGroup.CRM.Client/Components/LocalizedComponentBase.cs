using Microsoft.AspNetCore.Components;
using UniGroup.CRM.Client.Services;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Client.Components;

/// <summary>
/// Base class for components that render translated text.
/// Subscribes to <see cref="LanguageService.OnChange"/> so switching the
/// language instantly re-renders the component, and exposes translation helpers.
/// </summary>
public abstract class LocalizedComponentBase : ComponentBase, IDisposable
{
    /// <summary>The language service (current language + change notifications).</summary>
    [Inject] protected LanguageService Lang { get; set; } = default!;

    /// <summary>True when the current language is right-to-left (Arabic).</summary>
    protected bool IsRtl => Lang.IsRtl;

    /// <summary>Translates a resource key using the current language.</summary>
    protected string T(string key) => TranslationResources.Get(key, Lang.Current);

    /// <summary>Translates a resource key and formats it with the supplied arguments.</summary>
    protected string T(string key, params object?[] args) =>
        string.Format(TranslationResources.Get(key, Lang.Current), args);

    /// <summary>Localized label for a ticket status enum-name string.</summary>
    protected string TStatus(string status) => TranslationResources.Get($"Status.{status}", Lang.Current);

    /// <summary>Localized label for a ticket priority enum-name string.</summary>
    protected string TPriority(string priority) => TranslationResources.Get($"Priority.{priority}", Lang.Current);

    /// <summary>Localized label for a ticket category.</summary>
    protected string TCategory(TicketCategory category) => TranslationResources.Get($"Category.{category}", Lang.Current);

    /// <summary>Localized label for a ticket category enum-name string (falls back to raw value).</summary>
    protected string TCategory(string category) =>
        Enum.TryParse<TicketCategory>(category, out var c) ? TCategory(c) : category;

    /// <summary>Localized label for a warranty status string ("Active"/"Expired").</summary>
    protected string TWarranty(string warrantyStatus) =>
        TranslationResources.Get($"Warranty.{warrantyStatus}", Lang.Current);

    /// <summary>Localized "just now" / "5m ago" / date relative-time formatting.</summary>
    protected string TimeAgo(DateTime utc) => UiHelpers.TimeAgo(utc, Lang.Current);

    /// <inheritdoc />
    protected override void OnInitialized() => Lang.OnChange += HandleLanguageChanged;

    private void HandleLanguageChanged() => InvokeAsync(StateHasChanged);

    /// <summary>Unsubscribes from language change notifications.</summary>
    public virtual void Dispose()
    {
        Lang.OnChange -= HandleLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
