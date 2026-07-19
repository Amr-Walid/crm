using System;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Domain.Entities;

/// <summary>
/// Represents a phone call record in the CRM system, capturing both inbound
/// and outbound interactions between call center agents and customers.
/// </summary>
public class Call
{
    /// <summary>
    /// Gets or sets the unique identifier for this call record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the optional customer identifier.
    /// Null when the caller is not yet registered in the system.
    /// </summary>
    public Guid? CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the optional ticket identifier this call is related to.
    /// Null when the call is not linked to a specific support ticket.
    /// </summary>
    public string? TicketId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the agent who handled this call.
    /// </summary>
    public Guid AgentId { get; set; }

    /// <summary>
    /// Gets or sets the direction of the call (Inbound or Outbound).
    /// </summary>
    public CallDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets the phone number involved in this call.
    /// For inbound calls this is the caller's number; for outbound calls
    /// this is the number that was dialled.
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total duration of the call in seconds.
    /// </summary>
    public int DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets an optional free-text summary or notes written by the agent
    /// after the call ends.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Gets or sets an optional URL pointing to the call recording file
    /// stored on an external storage service (e.g., AWS S3 / Azure Blobs).
    /// </summary>
    public string? RecordingUrl { get; set; }

    /// <summary>
    /// Gets or sets the optional high-level (main) classification of the call
    /// (Maintenance, Complaint, or General Support). Null when unclassified.
    /// </summary>
    public MainCategory? MainCategory { get; set; }

    /// <summary>
    /// Gets or sets the optional sub-category classification of the call.
    /// Null when unclassified. When set it must belong to <see cref="MainCategory"/>.
    /// </summary>
    public TicketCategory? SubCategory { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when this call record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the customer associated with this call. May be null for
    /// unknown callers.
    /// </summary>
    public virtual Customer? Customer { get; set; }

    /// <summary>
    /// Gets or sets the ticket this call is linked to. May be null when the
    /// call is a general inquiry not tied to a specific ticket.
    /// </summary>
    public virtual Ticket? Ticket { get; set; }

    /// <summary>
    /// Gets or sets the application user (agent) who handled the call.
    /// </summary>
    public virtual ApplicationUser Agent { get; set; } = null!;
}
