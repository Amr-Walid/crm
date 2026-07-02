using System;

namespace UniGroup.CRM.Application.Features.Tickets.Queries.Common;

/// <summary>
/// DTO representing a ticket file attachment.
/// </summary>
public record AttachmentDto(
    Guid Id,
    string UploadedByName,
    string FileName,
    string StorageUrl,
    long FileSizeBytes,
    string ContentType,
    DateTime CreatedAt
);
