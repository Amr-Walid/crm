using System;

namespace UniGroup.CRM.Application.Features.Tickets.Queries.Common;

/// <summary>
/// DTO representing an internal note attached to a ticket.
/// </summary>
public record InternalNoteDto(
    Guid Id,
    string AuthorName,
    string Content,
    bool IsEdited,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
