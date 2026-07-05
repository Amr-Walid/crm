using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.Dashboards.Queries.Common;
using UniGroup.CRM.Domain.Entities;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Application.Features.Dashboards.Queries.GetAgentPerformance;

/// <summary>
/// Query to retrieve agent performance statistics.
/// </summary>
public record GetAgentPerformanceQuery(
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    Guid? AgentId = null
) : IRequest<List<AgentPerformanceDto>>;

/// <summary>
/// Handler for processing the GetAgentPerformanceQuery.
/// </summary>
public class GetAgentPerformanceQueryHandler : IRequestHandler<GetAgentPerformanceQuery, List<AgentPerformanceDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly HybridCache _hybridCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetAgentPerformanceQueryHandler"/> class.
    /// </summary>
    public GetAgentPerformanceQueryHandler(IApplicationDbContext context, HybridCache hybridCache)
    {
        _context = context;
        _hybridCache = hybridCache;
    }

    /// <inheritdoc />
    public async Task<List<AgentPerformanceDto>> Handle(GetAgentPerformanceQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"agent-performance-{request.DateFrom?.ToString("yyyy-MM-dd") ?? "all"}-{request.DateTo?.ToString("yyyy-MM-dd") ?? "all"}-{request.AgentId?.ToString() ?? "all"}";

#pragma warning disable EXTEXP0018
        return await _hybridCache.GetOrCreateAsync(
            cacheKey,
            async ct => await BuildPerformanceAsync(request, ct),
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5) },
            cancellationToken: cancellationToken
        );
#pragma warning restore EXTEXP0018
    }

    private async Task<List<AgentPerformanceDto>> BuildPerformanceAsync(GetAgentPerformanceQuery request, CancellationToken cancellationToken)
    {
        var dbContext = (DbContext)_context;
        var queryUsers = dbContext.Set<ApplicationUser>().AsNoTracking();

        if (request.AgentId.HasValue)
        {
            queryUsers = queryUsers.Where(u => u.Id == request.AgentId.Value);
        }

        var agents = await queryUsers.ToListAsync(cancellationToken);

        // Fetch closed tickets for performance calculation in the given timeframe
        var closedTicketsQuery = _context.Tickets
            .AsNoTracking()
            .Where(t => t.Status == TicketStatus.Closed && t.AssignedToId != null);

        if (request.DateFrom.HasValue)
        {
            closedTicketsQuery = closedTicketsQuery.Where(t => t.ClosedAt >= request.DateFrom.Value);
        }

        if (request.DateTo.HasValue)
        {
            closedTicketsQuery = closedTicketsQuery.Where(t => t.ClosedAt <= request.DateTo.Value);
        }

        var closedTickets = await closedTicketsQuery
            .Select(t => new { t.AssignedToId, t.CreatedAt, t.ClosedAt, t.TotalPausedSeconds, t.SlaBreached })
            .ToListAsync(cancellationToken);

        // Fetch open tickets count (current open state does not depend on closed date filters)
        var openTicketsCounts = await _context.Tickets
            .AsNoTracking()
            .Where(t => t.Status != TicketStatus.Closed && 
                         t.Status != TicketStatus.Cancelled && 
                         t.Status != TicketStatus.Resolved && 
                         t.AssignedToId != null)
            .GroupBy(t => t.AssignedToId)
            .Select(g => new { AgentId = g.Key!.Value, Count = g.Count() })
            .ToDictionaryAsync(x => x.AgentId, x => x.Count, cancellationToken);

        var performanceList = new List<AgentPerformanceDto>();

        foreach (var agent in agents)
        {
            var agentClosed = closedTickets.Where(t => t.AssignedToId == agent.Id).ToList();
            var totalClosed = agentClosed.Count;

            double avgResolutionTimeHours = 0.0;
            double slaComplianceRate = 100.0;

            if (totalClosed > 0)
            {
                var totalHours = agentClosed.Sum(t =>
                {
                    var elapsedSeconds = (t.ClosedAt!.Value - t.CreatedAt).TotalSeconds - t.TotalPausedSeconds;
                    return elapsedSeconds > 0 ? elapsedSeconds / 3600.0 : 0.0;
                });
                avgResolutionTimeHours = totalHours / totalClosed;

                var compliantCount = agentClosed.Count(t => !t.SlaBreached);
                slaComplianceRate = (compliantCount * 100.0) / totalClosed;
            }

            openTicketsCounts.TryGetValue(agent.Id, out var openCount);

            performanceList.Add(new AgentPerformanceDto(
                agent.Id,
                $"{agent.FirstName} {agent.LastName}",
                totalClosed,
                avgResolutionTimeHours,
                slaComplianceRate,
                CsatAvgScore: null, // CSAT averages will return null as Phase 6 CSAT surveys are not implemented yet.
                openCount
            ));
        }

        return performanceList;
    }
}
