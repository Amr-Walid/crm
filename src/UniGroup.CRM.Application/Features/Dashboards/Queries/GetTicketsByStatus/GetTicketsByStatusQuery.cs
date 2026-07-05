using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Application.Features.Dashboards.Queries.GetTicketsByStatus;

/// <summary>
/// Query to retrieve ticket count distribution by status.
/// </summary>
public record GetTicketsByStatusQuery(
    Guid? DepartmentId = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null
) : IRequest<Dictionary<TicketStatus, int>>;

/// <summary>
/// Handler for processing the GetTicketsByStatusQuery.
/// </summary>
public class GetTicketsByStatusQueryHandler : IRequestHandler<GetTicketsByStatusQuery, Dictionary<TicketStatus, int>>
{
    private readonly IApplicationDbContext _context;
    private readonly HybridCache _hybridCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTicketsByStatusQueryHandler"/> class.
    /// </summary>
    public GetTicketsByStatusQueryHandler(IApplicationDbContext context, HybridCache hybridCache)
    {
        _context = context;
        _hybridCache = hybridCache;
    }

    /// <inheritdoc />
    public async Task<Dictionary<TicketStatus, int>> Handle(GetTicketsByStatusQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"tickets-by-status-{request.DepartmentId?.ToString() ?? "all"}-{request.DateFrom?.ToString("yyyy-MM-dd") ?? "all"}-{request.DateTo?.ToString("yyyy-MM-dd") ?? "all"}";

#pragma warning disable EXTEXP0018
        return await _hybridCache.GetOrCreateAsync(
            cacheKey,
            async ct => await BuildTicketsByStatusAsync(request, ct),
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(60) },
            cancellationToken: cancellationToken
        );
#pragma warning restore EXTEXP0018
    }

    private async Task<Dictionary<TicketStatus, int>> BuildTicketsByStatusAsync(GetTicketsByStatusQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Tickets.AsNoTracking();

        if (request.DepartmentId.HasValue)
        {
            query = query.Where(t => t.DepartmentId == request.DepartmentId.Value);
        }

        if (request.DateFrom.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= request.DateFrom.Value);
        }

        if (request.DateTo.HasValue)
        {
            query = query.Where(t => t.CreatedAt <= request.DateTo.Value);
        }

        var counts = await query
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        // Ensure all statuses are mapped in the output dictionary
        var result = Enum.GetValues<TicketStatus>()
            .ToDictionary(
                status => status, 
                status => counts.TryGetValue(status, out var count) ? count : 0
            );

        return result;
    }
}
