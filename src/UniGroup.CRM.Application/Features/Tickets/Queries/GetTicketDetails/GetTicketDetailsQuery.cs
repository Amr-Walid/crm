using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.Tickets.Queries.Common;

namespace UniGroup.CRM.Application.Features.Tickets.Queries.GetTicketDetails;

/// <summary>
/// Query to get complete details of a ticket.
/// </summary>
public record GetTicketDetailsQuery(string TicketId) : IRequest<TicketDetailsDto>;

/// <summary>
/// Handler for executing the get ticket details query.
/// </summary>
public class GetTicketDetailsQueryHandler : IRequestHandler<GetTicketDetailsQuery, TicketDetailsDto>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTicketDetailsQueryHandler"/> class.
    /// </summary>
    public GetTicketDetailsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<TicketDetailsDto> Handle(GetTicketDetailsQuery request, CancellationToken cancellationToken)
    {
        var ticket = await _context.Tickets
            .Include(t => t.Customer)
            .Include(t => t.CustomerDevice!)
                .ThenInclude(d => d.Model)
                    .ThenInclude(m => m.Brand)
            .Include(t => t.AssignedTo)
            .Include(t => t.Department)
            .Include(t => t.Histories)
                .ThenInclude(h => h.ChangedBy)
            .Include(t => t.InternalNotes)
                .ThenInclude(n => n.Author)
            .Include(t => t.Attachments)
                .ThenInclude(a => a.UploadedBy)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket == null)
        {
            throw new Exception($"Ticket with ID {request.TicketId} was not found.");
        }

        // Get primary phone number of customer
        var customerPhone = await _context.CustomerPhones
            .Where(p => p.CustomerId == ticket.CustomerId && p.IsPrimary)
            .Select(p => p.Phone)
            .FirstOrDefaultAsync(cancellationToken);

        // Map histories
        var histories = ticket.Histories
            .OrderBy(h => h.CreatedAt)
            .Select(h => new TicketHistoryDto(
                h.Id,
                h.FromStatus?.ToString(),
                h.ToStatus.ToString(),
                $"{h.ChangedBy.FirstName} {h.ChangedBy.LastName}",
                h.Note,
                h.TimeInStatus,
                h.CreatedAt
            ))
            .ToList();

        // Map internal notes
        var notes = ticket.InternalNotes
            .OrderBy(n => n.CreatedAt)
            .Select(n => new InternalNoteDto(
                n.Id,
                $"{n.Author.FirstName} {n.Author.LastName}",
                n.Content,
                n.IsEdited,
                n.CreatedAt,
                n.UpdatedAt
            ))
            .ToList();

        // Map attachments
        var attachments = ticket.Attachments
            .OrderBy(a => a.CreatedAt)
            .Select(a => new AttachmentDto(
                a.Id,
                $"{a.UploadedBy.FirstName} {a.UploadedBy.LastName}",
                a.FileName,
                a.StorageUrl,
                a.FileSizeBytes,
                a.ContentType,
                a.CreatedAt
            ))
            .ToList();

        // Map device details safely
        string? modelName = ticket.CustomerDevice?.Model?.Name;
        string? brandName = ticket.CustomerDevice?.Model?.Brand?.Name;
        string? serialNumber = ticket.CustomerDevice?.SerialNumber;
        string? imei = ticket.CustomerDevice?.IMEI;

        return new TicketDetailsDto(
            ticket.Id,
            ticket.Title,
            ticket.Description,
            ticket.Category.ToString(),
            ticket.Status.ToString(),
            ticket.Priority.ToString(),
            ticket.CustomerId,
            ticket.Customer.Name,
            ticket.Customer.Email,
            customerPhone,
            ticket.CustomerDeviceId,
            modelName,
            brandName,
            serialNumber,
            imei,
            ticket.AssignedToId,
            ticket.AssignedTo != null ? $"{ticket.AssignedTo.FirstName} {ticket.AssignedTo.LastName}" : null,
            ticket.DepartmentId,
            ticket.Department?.Name,
            ticket.SlaDeadline,
            ticket.SlaBreached,
            ticket.SlaPausedAt,
            ticket.TotalPausedSeconds,
            ticket.ResolutionNote,
            ticket.ChatwootConversationId,
            ticket.CreatedAt,
            ticket.UpdatedAt,
            ticket.ClosedAt,
            histories,
            notes,
            attachments
        );
    }
}
