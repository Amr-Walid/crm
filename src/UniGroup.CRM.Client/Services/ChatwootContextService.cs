using Microsoft.JSInterop;

namespace UniGroup.CRM.Client.Services;

/// <summary>
/// Application-wide holder for the live Chatwoot conversation context. When the
/// CRM runs embedded as a Chatwoot Dashboard App, Chatwoot posts an
/// <c>appContext</c> message to the iframe every time the agent opens or
/// switches a conversation. The JS bridge in <c>index.html</c> forwards that
/// payload here, and any CRM page (Customers, TicketCreate, …) can subscribe to
/// <see cref="OnContextChanged"/> to auto-search / pre-fill against the active
/// customer — no manual copy/paste of phone numbers or IDs.
/// </summary>
/// <remarks>
/// Registered as a singleton so the context is shared by every page and
/// survives client-side navigation. A single <see cref="DotNetObjectReference{T}"/>
/// is registered with the JS bridge for the app's lifetime.
/// </remarks>
public sealed class ChatwootContextService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<ChatwootContextService>? _selfRef;
    private bool _registered;

    public ChatwootContextService(IJSRuntime js) => _js = js;

    /// <summary>Active Chatwoot conversation id (string form), if any.</summary>
    public string? ConversationId { get; private set; }

    /// <summary>Active Chatwoot contact id (string form), if any.</summary>
    public string? ContactId { get; private set; }

    /// <summary>Contact display name as known to Chatwoot.</summary>
    public string? ContactName { get; private set; }

    /// <summary>Contact phone number (E.164 as delivered by Chatwoot).</summary>
    public string? ContactPhone { get; private set; }

    /// <summary>Contact email as known to Chatwoot.</summary>
    public string? ContactEmail { get; private set; }

    /// <summary>True once any conversation context has been received.</summary>
    public bool HasContext =>
        !string.IsNullOrEmpty(ConversationId)
        || !string.IsNullOrEmpty(ContactPhone)
        || !string.IsNullOrEmpty(ContactEmail);

    /// <summary>
    /// Raised whenever a new/changed Chatwoot context arrives. Subscribers
    /// should call <c>InvokeAsync(StateHasChanged)</c> since this fires from a
    /// JS interop callback thread.
    /// </summary>
    public event Action? OnContextChanged;

    /// <summary>
    /// Registers this service with the JS bridge so Chatwoot context is pushed
    /// in. Safe to call multiple times; only the first call wires up interop.
    /// Buffered context (posted before Blazor booted) is replayed immediately.
    /// </summary>
    public async Task EnsureRegisteredAsync()
    {
        if (_registered) return;
        _registered = true;
        _selfRef = DotNetObjectReference.Create(this);
        try
        {
            await _js.InvokeVoidAsync("chatwootBridge.register", _selfRef);
        }
        catch
        {
            // JS bridge missing (e.g. non-embedded dev run) — harmless.
        }
    }

    /// <summary>Asks Chatwoot to (re)send the current conversation info.</summary>
    public async Task RequestContextAsync()
    {
        try { await _js.InvokeVoidAsync("chatwootBridge.requestInfo"); }
        catch { /* not embedded */ }
    }

    /// <summary>
    /// Copies <paramref name="text"/> to the OS clipboard so the agent can paste
    /// it into any field. Returns <c>true</c> on success.
    /// </summary>
    public async Task<bool> CopyToClipboardAsync(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        try { return await _js.InvokeAsync<bool>("unigroup.copyText", text); }
        catch { return false; }
    }

    /// <summary>
    /// Sends <paramref name="text"/> straight into the Chatwoot agent reply
    /// editor (when embedded) AND copies it to the clipboard as a guaranteed
    /// fallback. Returns <c>true</c> if either path succeeded so the UI can
    /// surface a confirmation toast.
    /// </summary>
    public async Task<bool> SendToReplyAsync(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        var pasted = false;
        try { pasted = await _js.InvokeAsync<bool>("chatwootBridge.pasteToReply", text); }
        catch { pasted = false; }
        var copied = await CopyToClipboardAsync(text);
        return pasted || copied;
    }

    /// <summary>Payload shape produced by the JS bridge in index.html.</summary>
    public sealed class ChatwootContextDto
    {
        public string? ConversationId { get; set; }
        public string? ContactId { get; set; }
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
    }

    /// <summary>
    /// Invoked by the JS bridge each time Chatwoot posts <c>appContext</c>.
    /// Updates the cached context and notifies subscribers only when something
    /// meaningful changed (avoids redundant re-renders / re-searches).
    /// </summary>
    [JSInvokable]
    public void OnChatwootContext(ChatwootContextDto ctx)
    {
        var changed =
            ctx.ConversationId != ConversationId ||
            ctx.ContactId != ContactId ||
            ctx.ContactPhone != ContactPhone ||
            ctx.ContactEmail != ContactEmail ||
            ctx.ContactName != ContactName;

        ConversationId = ctx.ConversationId;
        ContactId = ctx.ContactId;
        ContactName = ctx.ContactName;
        ContactPhone = ctx.ContactPhone;
        ContactEmail = ctx.ContactEmail;

        if (changed)
        {
            OnContextChanged?.Invoke();
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_registered)
            {
                await _js.InvokeVoidAsync("chatwootBridge.unregister");
            }
        }
        catch
        {
            // JS runtime may already be gone during teardown.
        }
        _selfRef?.Dispose();
        _selfRef = null;
    }
}
