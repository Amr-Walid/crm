using Microsoft.AspNetCore.Components;

namespace UniGroup.CRM.Client.Services;

/// <summary>
/// Detects whether the Blazor client is running embedded inside a Chatwoot
/// Dashboard-App iframe. Embedded mode is signalled by <c>?embedded=true</c>
/// on the URL (Chatwoot loads the app with a fixed base URL, so the flag is
/// carried once and cached for the lifetime of the SPA session).
/// </summary>
/// <remarks>
/// Registered as a singleton: once the app is loaded inside Chatwoot the mode
/// never changes, and the flag must survive client-side navigations that drop
/// the query string. <see cref="MainLayout"/> and the CRM pages read
/// <see cref="IsEmbedded"/> to adapt their chrome and behaviour.
/// </remarks>
public sealed class EmbeddedStateService
{
    private bool _initialized;

    /// <summary>True when the app is hosted inside the Chatwoot iframe.</summary>
    public bool IsEmbedded { get; private set; }

    /// <summary>
    /// Raised once when embedded mode is first detected, so components that
    /// rendered before initialization can re-render into their embedded chrome.
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// Inspects the current absolute URI for the <c>embedded=true</c> flag.
    /// Idempotent: the flag "sticks" for the session — subsequent navigations
    /// without the query string do not turn embedded mode back off.
    /// </summary>
    public void Initialize(NavigationManager nav)
    {
        if (_initialized) return;
        _initialized = true;

        IsEmbedded = QueryStringHelper.GetBool(nav.Uri, "embedded");
        if (IsEmbedded)
        {
            OnChange?.Invoke();
        }
    }

    /// <summary>
    /// Forces embedded mode on. Used by the JS bridge fallback when Chatwoot
    /// context arrives even though the query flag was stripped by a redirect.
    /// </summary>
    public void ForceEmbedded()
    {
        _initialized = true;
        if (IsEmbedded) return;
        IsEmbedded = true;
        OnChange?.Invoke();
    }
}
