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

namespace UniGroup.CRM.Application.Features.Dashboards.Queries.GetHourlyCallVolume;

/// <summary>
/// Query to retrieve the hourly call volume statistics.
/// </summary>
public record GetHourlyCallVolumeQuery(DateTime? Date = null) : IRequest<List<HourlyCallVolumeDto>>;

/// <summary>
/// Handler for processing the GetHourlyCallVolumeQuery.
/// </summary>
public class GetHourlyCallVolumeQueryHandler : IRequestHandler<GetHourlyCallVolumeQuery, List<HourlyCallVolumeDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly HybridCache _hybridCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetHourlyCallVolumeQueryHandler"/> class.
    /// </summary>
    public GetHourlyCallVolumeQueryHandler(IApplicationDbContext context, HybridCache hybridCache)
    {
        _context = context;
        _hybridCache = hybridCache;
    }

    /// <inheritdoc />
    public async Task<List<HourlyCallVolumeDto>> Handle(GetHourlyCallVolumeQuery request, CancellationToken cancellationToken)
    {
        var targetDate = request.Date ?? DateTime.UtcNow;
        var dateStart = targetDate.Date;
        var cacheKey = $"hourly-call-volume-{dateStart:yyyy-MM-dd}";

        return await _hybridCache.GetOrCreateAsync(
            cacheKey,
            async ct => await BuildHourlyCallVolumeAsync(dateStart, ct),
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5) },
            tags: new[] { "dashboard", "calls" },
            cancellationToken: cancellationToken
        );
    }

    private async Task<List<HourlyCallVolumeDto>> BuildHourlyCallVolumeAsync(DateTime dateStart, CancellationToken cancellationToken)
    {
        var dateEnd = dateStart.AddDays(1);

        var calls = await _context.Calls
            .AsNoTracking()
            .Where(c => c.CreatedAt >= dateStart && c.CreatedAt < dateEnd)
            .Select(c => new { c.CreatedAt.Hour, c.DurationSeconds })
            .ToListAsync(cancellationToken);

        var hourGroups = calls
            .GroupBy(c => c.Hour)
            .ToDictionary(g => g.Key, g => new
            {
                Count = g.Count(),
                AvgDuration = g.Average(c => c.DurationSeconds)
            });

        // Always return exactly 24 elements corresponding to hours 0-23
        var hourlyVolume = Enumerable.Range(0, 24).Select(hour =>
        {
            if (hourGroups.TryGetValue(hour, out var data))
            {
                return new HourlyCallVolumeDto(hour, data.Count, data.AvgDuration);
            }
            return new HourlyCallVolumeDto(hour, 0, 0.0);
        }).ToList();

        return hourlyVolume;
    }
}
