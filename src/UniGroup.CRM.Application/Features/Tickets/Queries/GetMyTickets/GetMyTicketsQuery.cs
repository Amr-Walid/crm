using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.Tickets.Queries.Common;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Application.Features.Tickets.Queries.GetMyTickets;

/// <summary>
/// Query to get tickets assigned to the current agent.
/// </summary>
public record GetMyTicketsQuery(
    Guid AgentId,
    TicketStatus? Status
) : IRequest<List<TicketSummaryDto>>;

/// <summary>
/// Handler for executing the get my tickets query.
/// </summary>
public class GetMyTicketsQueryHandler : IRequestHandler<GetMyTicketsQuery, List<TicketSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetMyTicketsQueryHandler"/> class.
    /// </summary>
    public GetMyTicketsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<List<TicketSummaryDto>> Handle(GetMyTicketsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Tickets
            .Include(t => t.Customer)
            .Include(t => t.AssignedTo)
            .Include(t => t.Department)
            .Where(t => t.AssignedToId == request.AgentId)
            .AsNoTracking();

        if (request.Status.HasValue)
        {
            query = query.Where(t => t.Status == request.Status.Value);
        }

        var dbItems = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return dbItems.Select(t => new TicketSummaryDto(
            t.Id,
            t.Title,
            t.Category.ToString(),
            t.Status.ToString(),
            t.Priority.ToString(),
            t.Customer.Name,
            t.AssignedTo != null ? $"{t.AssignedTo.FirstName} {t.AssignedTo.LastName}" : null,
            t.Department?.Name,
            t.SlaDeadline,
            t.SlaBreached,
            t.CreatedAt,
            t.UpdatedAt,
            (int)t.MainCategory,
            t.MainCategory.ToString(),
            (int)t.Category
        )).ToList();
    }
}
