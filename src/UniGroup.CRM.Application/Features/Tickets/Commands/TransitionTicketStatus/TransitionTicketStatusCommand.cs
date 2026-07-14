using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.Notifications.Events;
using UniGroup.CRM.Domain.Entities;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Application.Features.Tickets.Commands.TransitionTicketStatus;

/// <summary>
/// Command to transition a ticket's status.
/// </summary>
public record TransitionTicketStatusCommand(
    string TicketId,
    TicketStatus NewStatus,
    Guid AgentId,
    string? Note
) : IRequest;

/// <summary>
/// Handler for executing the status transition command.
/// </summary>
public class TransitionTicketStatusCommandHandler : IRequestHandler<TransitionTicketStatusCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransitionTicketStatusCommandHandler"/> class.
    /// </summary>
    public TransitionTicketStatusCommandHandler(IApplicationDbContext context, IPublisher publisher)
    {
        _context = context;
        _publisher = publisher;
    }

    /// <inheritdoc />
    public async Task Handle(TransitionTicketStatusCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _context.Tickets
            .Include(t => t.Histories)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket == null)
        {
            throw new Exception($"Ticket with ID {request.TicketId} does not exist.");
        }

        var oldStatus = ticket.Status;
        var newStatus = request.NewStatus;

        if (oldStatus == newStatus)
        {
            return; // No transition needed
        }

        // Validate allowed transitions
        bool isValid = (oldStatus, newStatus) switch
        {
            (TicketStatus.New, TicketStatus.Open) => true,
            (TicketStatus.New, TicketStatus.Cancelled) => true,

            (TicketStatus.Open, TicketStatus.InProgress) => true,
            (TicketStatus.Open, TicketStatus.Cancelled) => true,

            (TicketStatus.InProgress, TicketStatus.WaitingForCustomer) => true,
            (TicketStatus.InProgress, TicketStatus.WaitingForParts) => true,
            (TicketStatus.InProgress, TicketStatus.Escalated) => true,
            (TicketStatus.InProgress, TicketStatus.Resolved) => true,

            (TicketStatus.WaitingForCustomer, TicketStatus.InProgress) => true,
            (TicketStatus.WaitingForCustomer, TicketStatus.Cancelled) => true,

            (TicketStatus.WaitingForParts, TicketStatus.InProgress) => true,
            (TicketStatus.WaitingForParts, TicketStatus.Cancelled) => true,

            (TicketStatus.Escalated, TicketStatus.InProgress) => true,
            (TicketStatus.Escalated, TicketStatus.Resolved) => true,

            (TicketStatus.Resolved, TicketStatus.Closed) => true,
            (TicketStatus.Resolved, TicketStatus.InProgress) => true,

            _ => false
        };

        if (!isValid)
        {
            throw new Exception($"Transition from {oldStatus} to {newStatus} is not allowed.");
        }

        var now = DateTime.UtcNow;

        // Calculate time spent in the previous status
        long? timeInStatus = null;
        var latestHistory = ticket.Histories
            .OrderByDescending(h => h.CreatedAt)
            .FirstOrDefault();

        if (latestHistory != null)
        {
            timeInStatus = (long)(now - latestHistory.CreatedAt).TotalSeconds;
        }
        else
        {
            timeInStatus = (long)(now - ticket.CreatedAt).TotalSeconds;
        }

        // Handle SLA Pause / Resume
        // 1. Pausing countdown
        if (newStatus == TicketStatus.WaitingForCustomer || newStatus == TicketStatus.WaitingForParts)
        {
            ticket.SlaPausedAt = now;
        }
        // 2. Resuming countdown
        else if ((oldStatus == TicketStatus.WaitingForCustomer || oldStatus == TicketStatus.WaitingForParts) && newStatus == TicketStatus.InProgress)
        {
            if (ticket.SlaPausedAt.HasValue)
            {
                var pausedSeconds = (long)(now - ticket.SlaPausedAt.Value).TotalSeconds;
                ticket.TotalPausedSeconds += pausedSeconds;
                ticket.SlaPausedAt = null;
            }
        }
        // If transitioning to any other status from a waiting state, clear the pause timestamp (e.g., cancelled)
        else if (ticket.SlaPausedAt.HasValue)
        {
            ticket.SlaPausedAt = null;
        }

        // Handle Closed timestamp
        if (newStatus == TicketStatus.Closed)
        {
            ticket.ClosedAt = now;
        }
        else
        {
            ticket.ClosedAt = null; // Reset if re-opened
        }

        // Update ticket properties
        ticket.Status = newStatus;
        ticket.UpdatedAt = now;

        // Add history directly to DbSet to avoid EF Core change-tracker confusion
        var history = new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            FromStatus = oldStatus,
            ToStatus = newStatus,
            ChangedById = request.AgentId,
            Note = request.Note ?? $"Status changed from {oldStatus} to {newStatus}.",
            TimeInStatus = timeInStatus,
            CreatedAt = now
        };

        _context.TicketHistories.Add(history);

        await _context.SaveChangesAsync(cancellationToken);

        // Phase 6: publish notification events AFTER data is persisted
        if (newStatus == TicketStatus.Resolved)
        {
            await _publisher.Publish(
                new TicketResolvedEvent(ticket.Id, ticket.CustomerId, ticket.ChatwootConversationId),
                cancellationToken);
        }
        else if (newStatus == TicketStatus.Closed)
        {
            await _publisher.Publish(
                new TicketClosedEvent(ticket.Id, ticket.CustomerId, ticket.ChatwootConversationId),
                cancellationToken);
        }
    }
}
