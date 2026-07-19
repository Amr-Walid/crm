using System;
using System.Collections.Generic;

namespace UniGroup.CRM.Application.Features.Tickets.Queries.Common;

/// <summary>
/// DTO representing the full details of a ticket, including history, notes, and attachments.
/// </summary>
public record TicketDetailsDto(
    string Id,
    string Title,
    string Description,
    string Category,
    string Status,
    string Priority,
    Guid CustomerId,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    Guid? CustomerDeviceId,
    string? CustomerDeviceModelName,
    string? CustomerDeviceBrandName,
    string? CustomerDeviceSerialNumber,
    string? CustomerDeviceIMEI,
    Guid? AssignedToId,
    string? AssignedToName,
    Guid? DepartmentId,
    string? DepartmentName,
    DateTime? SlaDeadline,
    bool SlaBreached,
    DateTime? SlaPausedAt,
    long TotalPausedSeconds,
    string? ResolutionNote,
    string? ChatwootConversationId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ClosedAt,
    List<TicketHistoryDto> Histories,
    List<InternalNoteDto> InternalNotes,
    List<AttachmentDto> Attachments,
    int MainCategory = 0,
    string MainCategoryName = "Maintenance",
    int SubCategory = 0
);
