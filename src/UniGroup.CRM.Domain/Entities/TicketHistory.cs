using System;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Domain.Entities;

/// <summary>
/// Represents a transition in the lifecycle of a ticket, recording status changes and durations.
/// </summary>
public class TicketHistory
{
    /// <summary>
    /// Gets or sets the unique identifier of the history record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the ticket.
    /// </summary>
    public string TicketId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ticket navigation property.
    /// </summary>
    public virtual Ticket Ticket { get; set; } = null!;

    /// <summary>
    /// Gets or sets the status of the ticket before this transition.
    /// </summary>
    public TicketStatus? FromStatus { get; set; }

    /// <summary>
    /// Gets or sets the status of the ticket after this transition.
    /// </summary>
    public TicketStatus ToStatus { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who performed this status change.
    /// </summary>
    public Guid ChangedById { get; set; }

    /// <summary>
    /// Gets or sets the user navigation property.
    /// </summary>
    public virtual ApplicationUser ChangedBy { get; set; } = null!;

    /// <summary>
    /// Gets or sets a note describing why the change occurred.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Gets or sets the time spent in the previous status, in seconds.
    /// </summary>
    public long? TimeInStatus { get; set; }

    /// <summary>
    /// Gets or sets when this history record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
