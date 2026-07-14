namespace UniGroup.CRM.Client.Services;

/// <summary>Toast severity levels.</summary>
public enum ToastLevel
{
    /// <summary>Success (green accent).</summary>
    Success,

    /// <summary>Error (red accent).</summary>
    Error,

    /// <summary>Informational (blue accent).</summary>
    Info,
}

/// <summary>A single toast message.</summary>
public record ToastMessage(Guid Id, string Text, ToastLevel Level);

/// <summary>
/// Lightweight in-app toast notification service with auto-dismiss.
/// </summary>
public class ToastService
{
    private readonly List<ToastMessage> _toasts = [];

    /// <summary>Currently visible toasts.</summary>
    public IReadOnlyList<ToastMessage> Toasts => _toasts;

    /// <summary>Raised whenever the toast list changes.</summary>
    public event Action? OnChange;

    /// <summary>Shows a success toast.</summary>
    public void Success(string text) => Show(text, ToastLevel.Success);

    /// <summary>Shows an error toast.</summary>
    public void Error(string text) => Show(text, ToastLevel.Error);

    /// <summary>Shows an info toast.</summary>
    public void Info(string text) => Show(text, ToastLevel.Info);

    /// <summary>Shows a toast that auto-dismisses after 4.5 seconds.</summary>
    public void Show(string text, ToastLevel level)
    {
        var toast = new ToastMessage(Guid.NewGuid(), text, level);
        _toasts.Add(toast);
        OnChange?.Invoke();
        _ = DismissLaterAsync(toast.Id);
    }

    /// <summary>Removes a toast immediately.</summary>
    public void Dismiss(Guid id)
    {
        _toasts.RemoveAll(t => t.Id == id);
        OnChange?.Invoke();
    }

    private async Task DismissLaterAsync(Guid id)
    {
        await Task.Delay(4500);
        Dismiss(id);
    }
}
