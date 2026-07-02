using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Domain.Entities;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Application.Features.Tickets.Commands.AssignTicket;

/// <summary>
/// Command to assign a ticket to a user (agent) or department.
/// </summary>
public record AssignTicketCommand(
    string TicketId,
    Guid? AssignedToId,
    Guid? DepartmentId,
    Guid AssignedById,
    string? Note
) : IRequest;

/// <summary>
/// Handler for executing the assign ticket command.
/// </summary>
public class AssignTicketCommandHandler : IRequestHandler<AssignTicketCommand>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssignTicketCommandHandler"/> class.
    /// </summary>
    public AssignTicketCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task Handle(AssignTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _context.Tickets
            .Include(t => t.Department)
            .Include(t => t.AssignedTo)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket == null)
        {
            throw new Exception($"Ticket with ID {request.TicketId} does not exist.");
        }

        // Validate assigned user exists using AsNoTracking to avoid polluting the change tracker
        string? agentName = null;
        if (request.AssignedToId.HasValue)
        {
            var agent = await _context.Tickets.Entry(ticket).Context.Set<ApplicationUser>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == request.AssignedToId.Value, cancellationToken);
            if (agent == null)
            {
                throw new Exception($"Agent with ID {request.AssignedToId.Value} does not exist.");
            }
            agentName = $"{agent.FirstName} {agent.LastName}";
        }

        // Validate department exists
        string? departmentName = null;
        if (request.DepartmentId.HasValue)
        {
            var dept = await _context.Departments
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == request.DepartmentId.Value, cancellationToken);
            if (dept == null)
            {
                throw new Exception($"Department with ID {request.DepartmentId.Value} does not exist.");
            }
            departmentName = dept.Name;
        }

        var oldDeptId = ticket.DepartmentId;

        // Perform assignment
        ticket.AssignedToId = request.AssignedToId;
        ticket.DepartmentId = request.DepartmentId;
        ticket.UpdatedAt = DateTime.UtcNow;

        // Construct history log note
        string logNote;
        if (oldDeptId != request.DepartmentId)
        {
            var prevDeptName = ticket.Department?.Name ?? "None";
            var nextDeptName = departmentName ?? "None";
            logNote = request.Note ?? $"Ticket transferred from department '{prevDeptName}' to '{nextDeptName}'.";
        }
        else
        {
            var nextAgentName = agentName ?? "None";
            logNote = request.Note ?? $"Ticket assigned to agent '{nextAgentName}'.";
        }

        // Add history directly to DbSet to avoid EF Core change-tracker confusion
        var history = new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            FromStatus = ticket.Status,
            ToStatus = ticket.Status,
            ChangedById = request.AssignedById,
            Note = logNote,
            TimeInStatus = null,
            CreatedAt = DateTime.UtcNow
        };

        _context.TicketHistories.Add(history);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
