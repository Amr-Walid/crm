using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Client.Services;

/// <summary>
/// Static presentation helpers: enum labels, badge colors, icons,
/// SLA math, and formatting utilities shared across pages.
/// </summary>
public static class UiHelpers
{
    /// <summary>SLA duration hours per priority (Low=120, Medium=72, High=24, Critical=4).</summary>
    public static readonly IReadOnlyDictionary<TicketPriority, int> SlaHours = new Dictionary<TicketPriority, int>
    {
        [TicketPriority.Low] = 120,
        [TicketPriority.Medium] = 72,
        [TicketPriority.High] = 24,
        [TicketPriority.Critical] = 4,
    };

    /// <summary>Human label for a ticket category.</summary>
    public static string CategoryLabel(TicketCategory c) => c switch
    {
        TicketCategory.ScreenDamage => "Screen Damage",
        TicketCategory.BatteryIssue => "Battery Issue",
        TicketCategory.ChargingPort => "Charging Port",
        TicketCategory.SoftwareIssue => "Software Issue",
        TicketCategory.NetworkConnectivity => "Network Connectivity",
        TicketCategory.CameraIssue => "Camera Issue",
        TicketCategory.SpeakerMicrophone => "Speaker / Microphone",
        TicketCategory.PhysicalDamage => "Physical Damage",
        TicketCategory.WarrantyInquiry => "Warranty Inquiry",
        TicketCategory.GeneralInquiry => "General Inquiry",
        _ => "Other",
    };

    /// <summary>Bootstrap Icons class name for a ticket category (e.g. "bi-phone").</summary>
    public static string CategoryIcon(TicketCategory c) => c switch
    {
        TicketCategory.ScreenDamage => "bi-phone",
        TicketCategory.BatteryIssue => "bi-battery-half",
        TicketCategory.ChargingPort => "bi-plug",
        TicketCategory.SoftwareIssue => "bi-cpu",
        TicketCategory.NetworkConnectivity => "bi-wifi",
        TicketCategory.CameraIssue => "bi-camera",
        TicketCategory.SpeakerMicrophone => "bi-volume-up",
        TicketCategory.PhysicalDamage => "bi-tools",
        TicketCategory.WarrantyInquiry => "bi-shield-check",
        TicketCategory.GeneralInquiry => "bi-chat-dots",
        _ => "bi-question-circle",
    };

    /// <summary>Human label for a status enum-name string (e.g. "InProgress" → "In Progress").</summary>
    public static string StatusLabel(string status) => status switch
    {
        "InProgress" => "In Progress",
        "WaitingForCustomer" => "Waiting for Customer",
        "WaitingForParts" => "Waiting for Parts",
        _ => status,
    };

    /// <summary>Badge CSS class for a ticket status string.</summary>
    public static string StatusBadge(string status) => status switch
    {
        "New" => "badge-blue",
        "Open" => "badge-indigo",
        "InProgress" => "badge-purple",
        "WaitingForCustomer" => "badge-amber",
        "WaitingForParts" => "badge-orange",
        "Escalated" => "badge-red",
        "Resolved" => "badge-green",
        "Closed" => "badge-slate",
        "Cancelled" => "badge-slate",
        _ => "badge-slate",
    };

    /// <summary>Badge CSS class for a ticket priority string.</summary>
    public static string PriorityBadge(string priority) => priority switch
    {
        "Low" => "badge-slate",
        "Medium" => "badge-blue",
        "High" => "badge-amber",
        "Critical" => "badge-red",
        _ => "badge-slate",
    };

    /// <summary>Badge CSS class for a warranty status ("Active"/"Expired").</summary>
    public static string WarrantyBadge(string warrantyStatus) =>
        string.Equals(warrantyStatus, "Active", StringComparison.OrdinalIgnoreCase) ? "badge-green" : "badge-red";

    /// <summary>Statuses in which the SLA countdown is paused.</summary>
    public static bool IsSlaPaused(string status) => status is "WaitingForCustomer" or "WaitingForParts";

    /// <summary>Terminal ticket statuses.</summary>
    public static bool IsTerminal(string status) => status is "Resolved" or "Closed" or "Cancelled";

    /// <summary>Allowed next statuses for the transition dropdown, per current status.</summary>
    public static IEnumerable<TicketStatus> NextStatuses(string current) => current switch
    {
        "New" => [TicketStatus.Open, TicketStatus.InProgress, TicketStatus.Cancelled],
        "Open" => [TicketStatus.InProgress, TicketStatus.Escalated, TicketStatus.Cancelled],
        "InProgress" => [TicketStatus.WaitingForCustomer, TicketStatus.WaitingForParts, TicketStatus.Escalated, TicketStatus.Resolved, TicketStatus.Cancelled],
        "WaitingForCustomer" => [TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Cancelled],
        "WaitingForParts" => [TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Cancelled],
        "Escalated" => [TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Cancelled],
        "Resolved" => [TicketStatus.Closed, TicketStatus.InProgress],
        _ => [],
    };

    /// <summary>Formats a remaining <see cref="TimeSpan"/> as "2d 04:31:22" / "04:31:22".</summary>
    public static string FormatCountdown(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero)
        {
            remaining = remaining.Negate();
        }

        return remaining.TotalDays >= 1
            ? $"{(int)remaining.TotalDays}d {remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}"
            : $"{remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
    }

    /// <summary>Formats bytes into a friendly size string.</summary>
    public static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):0.#} MB",
        >= 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes} B",
    };

    /// <summary>Formats seconds as "3m 21s".</summary>
    public static string FormatDuration(int seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s"
            : ts.TotalMinutes >= 1 ? $"{ts.Minutes}m {ts.Seconds}s" : $"{ts.Seconds}s";
    }

    /// <summary>"Just now" / "5m ago" / "3h ago" / date, from a UTC timestamp (English).</summary>
    public static string TimeAgo(DateTime utc) => TimeAgo(utc, "en");

    /// <summary>Localized relative time from a UTC timestamp ("en" / "ar").</summary>
    public static string TimeAgo(DateTime utc, string lang)
    {
        var span = DateTime.UtcNow - DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return span.TotalSeconds < 60 ? TranslationResources.Get("Time.JustNow", lang)
            : span.TotalMinutes < 60 ? string.Format(TranslationResources.Get("Time.MinutesAgo", lang), (int)span.TotalMinutes)
            : span.TotalHours < 24 ? string.Format(TranslationResources.Get("Time.HoursAgo", lang), (int)span.TotalHours)
            : span.TotalDays < 7 ? string.Format(TranslationResources.Get("Time.DaysAgo", lang), (int)span.TotalDays)
            : utc.ToLocalTime().ToString("dd MMM yyyy");
    }

    /// <summary>Uppercase initials for an avatar (max 2 chars).</summary>
    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}"
            : char.ToUpperInvariant(parts[0][0]).ToString();
    }
}
