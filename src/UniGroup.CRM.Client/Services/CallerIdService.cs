using UniGroup.CRM.Application.Features.Customers.Queries.Common;

namespace UniGroup.CRM.Client.Services;

/// <summary>
/// Coordinates the floating Caller-ID simulator widget: incoming-call state,
/// caller lookup result, and cross-component events (e.g. opening a
/// customer's 360° profile from anywhere in the app).
/// </summary>
public class CallerIdService
{
    /// <summary>The phone number of the simulated incoming call, when ringing/answered.</summary>
    public string? IncomingNumber { get; private set; }

    /// <summary>Caller profile resolved via caller-id lookup (null = unknown caller).</summary>
    public CustomerDetailsDto? Caller { get; private set; }

    /// <summary>Whether a call is currently ringing.</summary>
    public bool IsRinging { get; private set; }

    /// <summary>Whether a call has been answered and is in progress.</summary>
    public bool IsInCall { get; private set; }

    /// <summary>UTC time the active call was answered (for duration tracking).</summary>
    public DateTime? AnsweredAtUtc { get; private set; }

    /// <summary>Raised when widget state changes.</summary>
    public event Action? OnChange;

    /// <summary>Starts a simulated incoming call.</summary>
    public void Ring(string phoneNumber, CustomerDetailsDto? caller)
    {
        IncomingNumber = phoneNumber;
        Caller = caller;
        IsRinging = true;
        IsInCall = false;
        AnsweredAtUtc = null;
        OnChange?.Invoke();
    }

    /// <summary>Answers the ringing call.</summary>
    public void Answer()
    {
        IsRinging = false;
        IsInCall = true;
        AnsweredAtUtc = DateTime.UtcNow;
        OnChange?.Invoke();
    }

    /// <summary>Ends/declines the call and resets state.</summary>
    public void End()
    {
        IncomingNumber = null;
        Caller = null;
        IsRinging = false;
        IsInCall = false;
        AnsweredAtUtc = null;
        OnChange?.Invoke();
    }
}
