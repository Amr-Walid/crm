using System;

namespace UniGroup.CRM.Application.Features.Tickets.Queries.Common;

/// <summary>
/// DTO representing a change history log of a ticket.
/// </summary>
public record TicketHistoryDto(
    Guid Id,
    string? FromStatus,
    string ToStatus,
    string ChangedByName,
    string? Note,
    long? TimeInStatus,
    DateTime CreatedAt
);
