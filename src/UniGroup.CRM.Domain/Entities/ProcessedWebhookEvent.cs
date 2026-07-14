using System;

namespace UniGroup.CRM.Domain.Entities;

/// <summary>
/// Enforces webhook idempotency by logging unique Chatwoot event/message
/// identifiers, preventing duplicate processing under at-least-once delivery.
/// </summary>
public class ProcessedWebhookEvent
{
    /// <summary>
    /// Gets or sets the unique Chatwoot message/event identifier (primary key).
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the event was successfully processed.
    /// </summary>
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
