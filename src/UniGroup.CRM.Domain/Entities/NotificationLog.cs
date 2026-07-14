using System;

namespace UniGroup.CRM.Domain.Entities;

/// <summary>
/// Represents a record of every notification dispatched by the notification
/// engine, auditing delivery status across all channels.
/// </summary>
public class NotificationLog
{
    /// <summary>
    /// Gets or sets the unique identifier of the notification log entry.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the recipient type (Agent or Customer).
    /// </summary>
    public string RecipientType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target identifier (user id, phone number, or e-mail address).
    /// </summary>
    public string RecipientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the delivery channel (Email, WhatsApp, InApp).
    /// </summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the notification template type (e.g. TicketAssigned, SlaBreached, CsatSurvey).
    /// </summary>
    public string TemplateType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the delivery status (Sent or Failed).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the compiled message content that was sent.
    /// </summary>
    public string? MessageContent { get; set; }

    /// <summary>
    /// Gets or sets the failure details if delivery failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the dispatch attempt.
    /// </summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
