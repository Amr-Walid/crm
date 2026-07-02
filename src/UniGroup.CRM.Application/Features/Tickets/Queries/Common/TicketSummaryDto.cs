using System;

namespace UniGroup.CRM.Application.Features.Tickets.Queries.Common;

/// <summary>
/// DTO representing a summary of a ticket, used for list views.
/// </summary>
public record TicketSummaryDto(
    string Id,
    string Title,
    string Category,
    string Status,
    string Priority,
    string CustomerName,
    string? AssignedToName,
    string? DepartmentName,
    DateTime? SlaDeadline,
    bool SlaBreached,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
