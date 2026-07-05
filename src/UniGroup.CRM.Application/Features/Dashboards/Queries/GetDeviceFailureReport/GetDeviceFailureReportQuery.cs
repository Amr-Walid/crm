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

namespace UniGroup.CRM.Application.Features.Dashboards.Queries.GetDeviceFailureReport;

/// <summary>
/// Query to retrieve device failure report statistics.
/// </summary>
public record GetDeviceFailureReportQuery(
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    Guid? BrandId = null,
    Guid? ModelId = null
) : IRequest<List<DeviceFailureReportDto>>;

/// <summary>
/// Handler for processing the GetDeviceFailureReportQuery.
/// </summary>
public class GetDeviceFailureReportQueryHandler : IRequestHandler<GetDeviceFailureReportQuery, List<DeviceFailureReportDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly HybridCache _hybridCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetDeviceFailureReportQueryHandler"/> class.
    /// </summary>
    public GetDeviceFailureReportQueryHandler(IApplicationDbContext context, HybridCache hybridCache)
    {
        _context = context;
        _hybridCache = hybridCache;
    }

    /// <inheritdoc />
    public async Task<List<DeviceFailureReportDto>> Handle(GetDeviceFailureReportQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"device-failures-{request.DateFrom?.ToString("yyyy-MM-dd") ?? "all"}-{request.DateTo?.ToString("yyyy-MM-dd") ?? "all"}-{request.BrandId?.ToString() ?? "all"}-{request.ModelId?.ToString() ?? "all"}";

#pragma warning disable EXTEXP0018
        return await _hybridCache.GetOrCreateAsync(
            cacheKey,
            async ct => await BuildFailureReportAsync(request, ct),
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(10) },
            cancellationToken: cancellationToken
        );
#pragma warning restore EXTEXP0018
    }

    private async Task<List<DeviceFailureReportDto>> BuildFailureReportAsync(GetDeviceFailureReportQuery request, CancellationToken cancellationToken)
    {
        var ticketsQuery = _context.Tickets
            .AsNoTracking()
            .Where(t => t.CustomerDeviceId != null);

        if (request.DateFrom.HasValue)
        {
            ticketsQuery = ticketsQuery.Where(t => t.CreatedAt >= request.DateFrom.Value);
        }

        if (request.DateTo.HasValue)
        {
            ticketsQuery = ticketsQuery.Where(t => t.CreatedAt <= request.DateTo.Value);
        }

        if (request.BrandId.HasValue)
        {
            ticketsQuery = ticketsQuery.Where(t => t.CustomerDevice!.Model!.BrandId == request.BrandId.Value);
        }

        if (request.ModelId.HasValue)
        {
            ticketsQuery = ticketsQuery.Where(t => t.CustomerDevice!.ModelId == request.ModelId.Value);
        }

        var tickets = await ticketsQuery
            .Select(t => new
            {
                t.Id,
                t.Category,
                t.CustomerId,
                ModelId = t.CustomerDevice!.ModelId,
                ModelName = t.CustomerDevice!.Model!.Name,
                BrandName = t.CustomerDevice!.Model!.Brand!.Name,
                CustomerDeviceId = t.CustomerDeviceId!.Value
            })
            .ToListAsync(cancellationToken);

        var reportList = tickets
            .GroupBy(t => new { t.ModelId, t.ModelName, t.BrandName })
            .Select(g =>
            {
                var modelTickets = g.ToList();
                var failureCount = modelTickets.Count;

                // Determine the most common category of ticket
                var mostCommonCategory = modelTickets
                    .GroupBy(t => t.Category)
                    .OrderByDescending(cg => cg.Count())
                    .Select(cg => cg.Key.ToString())
                    .FirstOrDefault();

                // Repeat customer count: customers who have submitted > 1 ticket for a device of this model in this period
                var repeatCustomersCount = modelTickets
                    .GroupBy(t => new { t.CustomerId, t.CustomerDeviceId })
                    .Where(cg => cg.Count() > 1)
                    .Select(cg => cg.Key.CustomerId)
                    .Distinct()
                    .Count();

                return new DeviceFailureReportDto(
                    g.Key.ModelName,
                    g.Key.BrandName,
                    failureCount,
                    mostCommonCategory,
                    repeatCustomersCount
                );
            })
            .OrderByDescending(r => r.FailureCount)
            .ToList();

        return reportList;
    }
}
