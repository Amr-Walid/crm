using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.Tickets.Queries.Common;
using UniGroup.CRM.Domain.Entities;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Application.Features.Tickets.Queries.GetTicketsList;

/// <summary>
/// Container record for paged query results.
/// </summary>
public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);

/// <summary>
/// Query to retrieve a filtered, paged list of tickets.
/// </summary>
public record GetTicketsListQuery(
    TicketStatus? Status,
    TicketPriority? Priority,
    Guid? DepartmentId,
    Guid? AssignedToId,
    DateTime? DateFrom,
    DateTime? DateTo,
    int Page = 1,
    int PageSize = 10
) : IRequest<PagedResult<TicketSummaryDto>>;

/// <summary>
/// Handler for executing the get tickets list query.
/// </summary>
public class GetTicketsListQueryHandler : IRequestHandler<GetTicketsListQuery, PagedResult<TicketSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTicketsListQueryHandler"/> class.
    /// </summary>
    public GetTicketsListQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<PagedResult<TicketSummaryDto>> Handle(GetTicketsListQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Tickets
            .Include(t => t.Customer)
            .Include(t => t.AssignedTo)
            .Include(t => t.Department)
            .AsNoTracking();

        // Apply filters
        if (request.Status.HasValue)
        {
            query = query.Where(t => t.Status == request.Status.Value);
        }

        if (request.Priority.HasValue)
        {
            query = query.Where(t => t.Priority == request.Priority.Value);
        }

        if (request.DepartmentId.HasValue)
        {
            query = query.Where(t => t.DepartmentId == request.DepartmentId.Value);
        }

        if (request.AssignedToId.HasValue)
        {
            query = query.Where(t => t.AssignedToId == request.AssignedToId.Value);
        }

        if (request.DateFrom.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= request.DateFrom.Value);
        }

        if (request.DateTo.HasValue)
        {
            query = query.Where(t => t.CreatedAt <= request.DateTo.Value);
        }

        // Calculate total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Normalize pagination inputs
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;

        // Fetch paged data ordered by creation date descending
        var dbItems = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Map to DTOs
        var items = dbItems.Select(t => new TicketSummaryDto(
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
            t.UpdatedAt
        )).ToList();

        return new PagedResult<TicketSummaryDto>(items, totalCount, page, pageSize);
    }
}
