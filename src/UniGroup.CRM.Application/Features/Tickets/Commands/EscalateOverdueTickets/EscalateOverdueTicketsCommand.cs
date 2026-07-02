using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Domain.Entities;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Application.Features.Tickets.Commands.EscalateOverdueTickets;

/// <summary>
/// Command triggered by background worker to escalate overdue tickets.
/// </summary>
public record EscalateOverdueTicketsCommand : IRequest;

/// <summary>
/// Handler for executing the escalate overdue tickets command.
/// </summary>
public class EscalateOverdueTicketsCommandHandler : IRequestHandler<EscalateOverdueTicketsCommand>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="EscalateOverdueTicketsCommandHandler"/> class.
    /// </summary>
    public EscalateOverdueTicketsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task Handle(EscalateOverdueTicketsCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Query active tickets that have a deadline set and are not resolved/closed/cancelled/paused
        var activeStatuses = new[] { TicketStatus.New, TicketStatus.Open, TicketStatus.InProgress };
        
        var activeTickets = await _context.Tickets
            .Where(t => activeStatuses.Contains(t.Status) && t.SlaDeadline.HasValue)
            .ToListAsync(cancellationToken);

        var overdueTickets = activeTickets
            .Where(t => now > t.SlaDeadline.Value.AddSeconds(t.TotalPausedSeconds))
            .ToList();

        if (!overdueTickets.Any())
        {
            return;
        }

        // Retrieve a valid system user ID to satisfy the foreign key constraint on TicketHistory
        var systemUser = await _context.Tickets.Entry(overdueTickets.First()).Context.Set<ApplicationUser>()
            .OrderBy(u => u.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (systemUser == null)
        {
            // Cannot log history without at least one user in the database
            return;
        }

        foreach (var ticket in overdueTickets)
        {
            var oldStatus = ticket.Status;
            
            // Calculate time in status for the history record
            long? timeInStatus = null;
            var latestHistory = await _context.TicketHistories
                .Where(h => h.TicketId == ticket.Id)
                .OrderByDescending(h => h.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestHistory != null)
            {
                timeInStatus = (long)(now - latestHistory.CreatedAt).TotalSeconds;
            }
            else
            {
                timeInStatus = (long)(now - ticket.CreatedAt).TotalSeconds;
            }

            // Escalate the ticket
            ticket.Status = TicketStatus.Escalated;
            ticket.SlaBreached = true;
            ticket.UpdatedAt = now;

            var history = new TicketHistory
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                FromStatus = oldStatus,
                ToStatus = TicketStatus.Escalated,
                ChangedById = systemUser.Id,
                Note = "SLA deadline breached. Ticket automatically escalated.",
                TimeInStatus = timeInStatus,
                CreatedAt = now
            };

            _context.TicketHistories.Add(history);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
