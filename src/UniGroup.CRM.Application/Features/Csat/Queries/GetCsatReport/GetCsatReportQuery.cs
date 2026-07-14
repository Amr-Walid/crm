using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;

namespace UniGroup.CRM.Application.Features.Csat.Queries.GetCsatReport;

/// <summary>
/// Aggregated CSAT report for a date range.
/// </summary>
/// <param name="TotalSent">Total surveys dispatched in range.</param>
/// <param name="TotalSubmitted">Total surveys submitted in range.</param>
/// <param name="ResponseRate">Submission rate percentage (0-100).</param>
/// <param name="AverageRating">Average rating of submitted surveys.</param>
/// <param name="RatingDistribution">Count of submissions per rating value (1-5).</param>
public record CsatReportDto(
    int TotalSent,
    int TotalSubmitted,
    double ResponseRate,
    double AverageRating,
    Dictionary<int, int> RatingDistribution);

/// <summary>
/// Query returning the aggregated CSAT report, optionally filtered by sent-date range.
/// </summary>
/// <param name="From">Optional inclusive lower bound on SentAt.</param>
/// <param name="To">Optional inclusive upper bound on SentAt.</param>
public record GetCsatReportQuery(DateTime? From, DateTime? To) : IRequest<CsatReportDto>;

/// <summary>
/// Handler computing CSAT aggregates.
/// </summary>
public class GetCsatReportQueryHandler : IRequestHandler<GetCsatReportQuery, CsatReportDto>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetCsatReportQueryHandler"/> class.
    /// </summary>
    public GetCsatReportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<CsatReportDto> Handle(GetCsatReportQuery request, CancellationToken cancellationToken)
    {
        var query = _context.CsatSurveys.AsNoTracking().AsQueryable();

        if (request.From.HasValue)
        {
            query = query.Where(s => s.SentAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(s => s.SentAt <= request.To.Value);
        }

        var surveys = await query
            .Select(s => new { s.SubmittedAt, s.Rating })
            .ToListAsync(cancellationToken);

        var totalSent = surveys.Count;
        var submitted = surveys.Where(s => s.SubmittedAt.HasValue).ToList();
        var totalSubmitted = submitted.Count;

        var distribution = new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 } };
        foreach (var s in submitted.Where(s => s.Rating >= 1 && s.Rating <= 5))
        {
            distribution[s.Rating]++;
        }

        return new CsatReportDto(
            totalSent,
            totalSubmitted,
            totalSent == 0 ? 0 : Math.Round(totalSubmitted * 100.0 / totalSent, 2),
            totalSubmitted == 0 ? 0 : Math.Round(submitted.Average(s => s.Rating), 2),
            distribution);
    }
}
