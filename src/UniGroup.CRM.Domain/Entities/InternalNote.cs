using System;

namespace UniGroup.CRM.Domain.Entities;

/// <summary>
/// Represents an internal notes written on a ticket by customer service agents.
/// </summary>
public class InternalNote
{
    /// <summary>
    /// Gets or sets the unique identifier of the internal note.
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
    /// Gets or sets the identifier of the user (agent) who wrote the note.
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Gets or sets the author navigation property.
    /// </summary>
    public virtual ApplicationUser Author { get; set; } = null!;

    /// <summary>
    /// Gets or sets the text content of the note.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the note has been edited.
    /// </summary>
    public bool IsEdited { get; set; }

    /// <summary>
    /// Gets or sets when the note was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when the note was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
