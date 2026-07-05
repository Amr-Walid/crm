using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.Dashboards.Queries.Common;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Application.Features.Dashboards.Queries.GetDashboardSummary;

/// <summary>
/// Query to retrieve the dashboard summary statistics.
/// </summary>
public record GetDashboardSummaryQuery(DateTime? Date = null) : IRequest<DashboardSummaryDto>;

/// <summary>
/// Handler for processing the GetDashboardSummaryQuery.
/// </summary>
public class GetDashboardSummaryQueryHandler : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly HybridCache _hybridCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetDashboardSummaryQueryHandler"/> class.
    /// </summary>
    public GetDashboardSummaryQueryHandler(IApplicationDbContext context, HybridCache hybridCache)
    {
        _context = context;
        _hybridCache = hybridCache;
    }

    /// <inheritdoc />
    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var targetDate = request.Date ?? DateTime.UtcNow;
        var todayStart = targetDate.Date;
        var cacheKey = $"dashboard-summary-{todayStart:yyyy-MM-dd}";

        return await _hybridCache.GetOrCreateAsync(
            cacheKey,
            async ct => await BuildSummaryAsync(todayStart, ct),
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(60) },
            tags: new[] { "dashboard" },
            cancellationToken: cancellationToken
        );
    }

    private async Task<DashboardSummaryDto> BuildSummaryAsync(DateTime todayStart, CancellationToken cancellationToken)
    {
        var todayEnd = todayStart.AddDays(1);

        // 1. Today's new tickets
        var todayNewTickets = await _context.Tickets
            .AsNoTracking()
            .CountAsync(t => t.CreatedAt >= todayStart && t.CreatedAt < todayEnd, cancellationToken);

        // 2. Total open tickets (not closed, resolved, or cancelled)
        var openTicketsTotal = await _context.Tickets
            .AsNoTracking()
            .CountAsync(t => t.Status != TicketStatus.Resolved && 
                             t.Status != TicketStatus.Closed && 
                             t.Status != TicketStatus.Cancelled, cancellationToken);

        // 3. Breached SLA count (for non-closed, non-cancelled tickets)
        var breachedSlaCount = await _context.Tickets
            .AsNoTracking()
            .CountAsync(t => t.SlaBreached && 
                             t.Status != TicketStatus.Closed && 
                             t.Status != TicketStatus.Cancelled, cancellationToken);

        // 4. Resolved today tickets & SLA compliance rate
        var resolvedTodayTickets = await _context.Tickets
            .AsNoTracking()
            .Where(t => t.ClosedAt >= todayStart && t.ClosedAt < todayEnd && t.Status == TicketStatus.Closed)
            .Select(t => new { t.CreatedAt, t.ClosedAt, t.TotalPausedSeconds, t.SlaBreached })
            .ToListAsync(cancellationToken);

        double avgResolutionTimeToday = 0;
        double slaComplianceRate = 100.0;
        var closedTodayCount = resolvedTodayTickets.Count;

        if (closedTodayCount > 0)
        {
            var totalHours = resolvedTodayTickets.Sum(t =>
            {
                var elapsedSeconds = (t.ClosedAt!.Value - t.CreatedAt).TotalSeconds - t.TotalPausedSeconds;
                return elapsedSeconds > 0 ? elapsedSeconds / 3600.0 : 0.0;
            });
            avgResolutionTimeToday = totalHours / closedTodayCount;

            var compliantCount = resolvedTodayTickets.Count(t => !t.SlaBreached);
            slaComplianceRate = (compliantCount * 100.0) / closedTodayCount;
        }

        // 5. Calls logged today
        var callsLoggedToday = await _context.Calls
            .AsNoTracking()
            .CountAsync(c => c.CreatedAt >= todayStart && c.CreatedAt < todayEnd, cancellationToken);

        // 6. Top issue category
        var topCategoryGroup = await _context.Tickets
            .AsNoTracking()
            .Where(t => t.CreatedAt >= todayStart && t.CreatedAt < todayEnd)
            .GroupBy(t => t.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .FirstOrDefaultAsync(cancellationToken);

        string? topIssueCategory = topCategoryGroup?.Category.ToString();

        return new DashboardSummaryDto(
            todayNewTickets,
            openTicketsTotal,
            breachedSlaCount,
            avgResolutionTimeToday,
            callsLoggedToday,
            topIssueCategory,
            slaComplianceRate
        );
    }
}
