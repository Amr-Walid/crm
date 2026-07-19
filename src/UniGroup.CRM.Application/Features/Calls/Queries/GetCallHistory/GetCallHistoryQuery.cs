using MediatR;
using Microsoft.EntityFrameworkCore;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.Calls.Queries.Common;
using UniGroup.CRM.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UniGroup.CRM.Application.Features.Calls.Queries.GetCallHistory;

/// <summary>
/// Query to retrieve a paged list of call records for a given customer,
/// ordered by most recent first.
/// </summary>
/// <param name="CustomerId">The customer whose call history is requested.</param>
/// <param name="Page">The 1-based page number. Defaults to 1.</param>
/// <param name="PageSize">The number of records per page. Defaults to 20.</param>
public record GetCallHistoryQuery(Guid? CustomerId, int Page = 1, int PageSize = 20)
    : IRequest<List<CallDto>>;

/// <summary>
/// Handler for <see cref="GetCallHistoryQuery"/>.
/// Returns a paged, descending-ordered list of <see cref="CallDto"/> records.
/// </summary>
public class GetCallHistoryQueryHandler : IRequestHandler<GetCallHistoryQuery, List<CallDto>>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetCallHistoryQueryHandler"/> class.
    /// </summary>
    /// <param name="context">The application database context.</param>
    public GetCallHistoryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Handles the call history retrieval.
    /// </summary>
    /// <param name="request">The query specifying the customer and pagination parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A paged list of <see cref="CallDto"/> for the customer, newest first.</returns>
    /// <exception cref="ArgumentException">Thrown when page or pageSize values are out of range.</exception>
    public async Task<List<CallDto>> Handle(
        GetCallHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var calls = await _context.Calls
            .AsNoTracking()
            .Include(c => c.Customer)
            .Where(c => c.CustomerId == request.CustomerId)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CallDto(
                c.Id,
                c.CustomerId,
                c.Customer != null ? c.Customer.Name : null,
                c.AgentId,
                c.Direction,
                c.PhoneNumber,
                c.DurationSeconds,
                c.Summary,
                c.RecordingUrl,
                c.CreatedAt,
                (int?)c.MainCategory,
                null,
                (int?)c.SubCategory,
                null
            ))
            .ToListAsync(cancellationToken);

        // Resolve localizable enum names after materialization (avoids translating
        // nullable-enum ToString() in the SQL projection).
        return calls
            .Select(c => c with
            {
                MainCategoryName = c.MainCategory.HasValue
                    ? ((MainCategory)c.MainCategory.Value).ToString()
                    : null,
                SubCategoryName = c.SubCategory.HasValue
                    ? ((TicketCategory)c.SubCategory.Value).ToString()
                    : null
            })
            .ToList();
    }
}
