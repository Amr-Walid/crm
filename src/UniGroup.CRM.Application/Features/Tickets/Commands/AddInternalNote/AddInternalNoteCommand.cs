using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Domain.Entities;

namespace UniGroup.CRM.Application.Features.Tickets.Commands.AddInternalNote;

/// <summary>
/// Command to add an internal staff note to a ticket.
/// </summary>
public record AddInternalNoteCommand(
    string TicketId,
    string Content,
    Guid AuthorId
) : IRequest<Guid>;

/// <summary>
/// Handler for executing the add internal note command.
/// </summary>
public class AddInternalNoteCommandHandler : IRequestHandler<AddInternalNoteCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddInternalNoteCommandHandler"/> class.
    /// </summary>
    public AddInternalNoteCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<Guid> Handle(AddInternalNoteCommand request, CancellationToken cancellationToken)
    {
        var ticketExists = await _context.Tickets.AnyAsync(t => t.Id == request.TicketId, cancellationToken);
        if (!ticketExists)
        {
            throw new Exception($"Ticket with ID {request.TicketId} does not exist.");
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new Exception("Note content cannot be empty.");
        }

        var note = new InternalNote
        {
            Id = Guid.NewGuid(),
            TicketId = request.TicketId,
            AuthorId = request.AuthorId,
            Content = request.Content,
            IsEdited = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.InternalNotes.Add(note);

        // Update Ticket's UpdatedAt timestamp
        var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);
        if (ticket != null)
        {
            ticket.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return note.Id;
    }
}
