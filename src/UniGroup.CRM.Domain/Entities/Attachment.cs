using System;

namespace UniGroup.CRM.Domain.Entities;

/// <summary>
/// Represents a file attachment linked to a ticket (e.g. photos of damage, PDF receipts).
/// </summary>
public class Attachment
{
    /// <summary>
    /// Gets or sets the unique identifier of the attachment.
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
    /// Gets or sets the identifier of the user who uploaded the attachment.
    /// </summary>
    public Guid UploadedById { get; set; }

    /// <summary>
    /// Gets or sets the uploader navigation property.
    /// </summary>
    public virtual ApplicationUser UploadedBy { get; set; } = null!;

    /// <summary>
    /// Gets or sets the original name of the uploaded file.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URI/path where the file is stored.
    /// </summary>
    public string StorageUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the size of the file in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the MIME/media content type (e.g. image/png).
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the attachment was uploaded.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
