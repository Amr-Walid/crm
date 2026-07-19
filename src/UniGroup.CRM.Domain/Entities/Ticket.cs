using System;
using System.Collections.Generic;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Domain.Entities;

/// <summary>
/// Represents a support or service ticket in the CRM system.
/// </summary>
public class Ticket
{
    /// <summary>
    /// Gets or sets the human-readable identifier of the ticket (e.g. T-2026-00001).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique identifier of the customer associated with the ticket.
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the customer navigation property.
    /// </summary>
    public virtual Customer Customer { get; set; } = null!;

    /// <summary>
    /// Gets or sets the unique identifier of the customer device, if any.
    /// </summary>
    public Guid? CustomerDeviceId { get; set; }

    /// <summary>
    /// Gets or sets the customer device navigation property.
    /// </summary>
    public virtual CustomerDevice? CustomerDevice { get; set; }

    /// <summary>
    /// Gets or sets the ticket title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the detailed description of the ticket.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the high-level (main) category of the ticket
    /// (Maintenance, Complaint, or General Support).
    /// </summary>
    public MainCategory MainCategory { get; set; }

    /// <summary>
    /// Gets or sets the ticket category (treated as the sub-category under
    /// <see cref="MainCategory"/>).
    /// </summary>
    public TicketCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the ticket status.
    /// </summary>
    public TicketStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the ticket priority.
    /// </summary>
    public TicketPriority Priority { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user (agent) assigned to this ticket.
    /// </summary>
    public Guid? AssignedToId { get; set; }

    /// <summary>
    /// Gets or sets the assigned user navigation property.
    /// </summary>
    public virtual ApplicationUser? AssignedTo { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the department handling the ticket.
    /// </summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>
    /// Gets or sets the department navigation property.
    /// </summary>
    public virtual Department? Department { get; set; }

    /// <summary>
    /// Gets or sets the SLA deadline timestamp.
    /// </summary>
    public DateTime? SlaDeadline { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the SLA has been breached.
    /// </summary>
    public bool SlaBreached { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the SLA countdown was paused.
    /// </summary>
    public DateTime? SlaPausedAt { get; set; }

    /// <summary>
    /// Gets or sets the accumulated pause time in seconds.
    /// </summary>
    public long TotalPausedSeconds { get; set; }

    /// <summary>
    /// Gets or sets the resolution note provided when the ticket is closed/resolved.
    /// </summary>
    public string? ResolutionNote { get; set; }

    /// <summary>
    /// Gets or sets the Chatwoot conversation identifier.
    /// </summary>
    public string? ChatwootConversationId { get; set; }

    /// <summary>
    /// Gets or sets when the ticket was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when the ticket was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when the ticket was closed.
    /// </summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// Gets or sets the history records of the ticket transitions.
    /// </summary>
    public virtual ICollection<TicketHistory> Histories { get; set; } = new List<TicketHistory>();

    /// <summary>
    /// Gets or sets the internal staff notes for the ticket.
    /// </summary>
    public virtual ICollection<InternalNote> InternalNotes { get; set; } = new List<InternalNote>();

    /// <summary>
    /// Gets or sets the files attached to the ticket.
    /// </summary>
    public virtual ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}
