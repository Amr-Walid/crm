using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Domain.Entities;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Application.Features.Tickets.Commands.CreateTicket;

/// <summary>
/// Command to create a new ticket.
/// </summary>
public record CreateTicketCommand(
    Guid CustomerId,
    Guid? CustomerDeviceId,
    string Title,
    string Description,
    MainCategory MainCategory,
    TicketCategory Category,
    TicketPriority Priority,
    Guid CreatedById
) : IRequest<string>;

/// <summary>
/// Handler for executing the create ticket command.
/// </summary>
public class CreateTicketCommandHandler : IRequestHandler<CreateTicketCommand, string>
{
    private readonly IApplicationDbContext _context;
    private readonly ITicketNumberGenerator _numberGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTicketCommandHandler"/> class.
    /// </summary>
    public CreateTicketCommandHandler(IApplicationDbContext context, ITicketNumberGenerator numberGenerator)
    {
        _context = context;
        _numberGenerator = numberGenerator;
    }

    /// <inheritdoc />
    public async Task<string> Handle(CreateTicketCommand request, CancellationToken cancellationToken)
    {
        // Verify customer exists
        var customerExists = await _context.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken);
        if (!customerExists)
        {
            throw new Exception($"Customer with ID {request.CustomerId} does not exist.");
        }

        // Validate that the chosen sub-category belongs to the selected main category.
        if (!TicketCategoryMap.IsValidPair(request.MainCategory, request.Category))
        {
            throw new Exception(
                $"Sub-category '{request.Category}' is not valid for main category '{request.MainCategory}'.");
        }

        // Verify device exists and belongs to customer (if device is specified)
        if (request.CustomerDeviceId.HasValue)
        {
            var device = await _context.CustomerDevices
                .FirstOrDefaultAsync(d => d.Id == request.CustomerDeviceId.Value, cancellationToken);
            if (device == null)
            {
                throw new Exception($"Customer device with ID {request.CustomerDeviceId.Value} does not exist.");
            }
            if (device.CustomerId != request.CustomerId)
            {
                throw new Exception("The specified device does not belong to the selected customer.");
            }
        }

        // Calculate SLA Deadline based on priority
        var now = DateTime.UtcNow;
        var slaDuration = request.Priority switch
        {
            TicketPriority.Critical => TimeSpan.FromHours(4),
            TicketPriority.High => TimeSpan.FromHours(24),
            TicketPriority.Medium => TimeSpan.FromHours(72),
            TicketPriority.Low => TimeSpan.FromHours(120),
            _ => TimeSpan.FromHours(72)
        };
        var slaDeadline = now.Add(slaDuration);

        // Generate the consecutive ticket number and save. Number generation and
        // insertion are not one atomic step, so a concurrent writer (e.g. the
        // Chatwoot webhook processor) may claim the same number first — on a
        // unique-key collision we regenerate and retry (max 3 attempts).
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            var ticketNumber = await _numberGenerator.GenerateAsync(cancellationToken);

            var ticket = new Ticket
            {
                Id = ticketNumber,
                CustomerId = request.CustomerId,
                CustomerDeviceId = request.CustomerDeviceId,
                Title = request.Title,
                Description = request.Description,
                MainCategory = request.MainCategory,
                Category = request.Category,
                Status = TicketStatus.New,
                Priority = request.Priority,
                SlaDeadline = slaDeadline,
                SlaBreached = false,
                TotalPausedSeconds = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            var history = new TicketHistory
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                FromStatus = null,
                ToStatus = TicketStatus.New,
                ChangedById = request.CreatedById,
                Note = "Ticket created.",
                TimeInStatus = null,
                CreatedAt = now
            };

            ticket.Histories.Add(history);
            _context.Tickets.Add(ticket);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return ticket.Id;
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                // Detach the failed entities and retry with a freshly generated number.
                _context.Tickets.Entry(ticket).State = EntityState.Detached;
                _context.TicketHistories.Entry(history).State = EntityState.Detached;
            }
        }
    }
}
