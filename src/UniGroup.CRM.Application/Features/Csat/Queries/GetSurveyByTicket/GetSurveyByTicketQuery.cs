using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;

namespace UniGroup.CRM.Application.Features.Csat.Queries.GetSurveyByTicket;

/// <summary>
/// CSAT survey details for a single ticket (token excluded for security).
/// </summary>
/// <param name="Id">Survey id.</param>
/// <param name="TicketId">Related ticket id.</param>
/// <param name="CustomerId">Customer id.</param>
/// <param name="Rating">Submitted rating (0 = not yet submitted).</param>
/// <param name="Feedback">Optional feedback text.</param>
/// <param name="SentAt">Dispatch timestamp (UTC).</param>
/// <param name="ExpiresAt">Expiration timestamp (UTC).</param>
/// <param name="SubmittedAt">Submission timestamp (UTC), null when pending.</param>
public record CsatSurveyDto(
    Guid Id,
    string TicketId,
    Guid CustomerId,
    int Rating,
    string? Feedback,
    DateTime SentAt,
    DateTime ExpiresAt,
    DateTime? SubmittedAt);

/// <summary>
/// Query returning the CSAT survey for a specific ticket, or null when none exists.
/// </summary>
/// <param name="TicketId">The ticket id to look up.</param>
public record GetSurveyByTicketQuery(string TicketId) : IRequest<CsatSurveyDto?>;

/// <summary>
/// Handler fetching the survey by ticket id.
/// </summary>
public class GetSurveyByTicketQueryHandler : IRequestHandler<GetSurveyByTicketQuery, CsatSurveyDto?>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetSurveyByTicketQueryHandler"/> class.
    /// </summary>
    public GetSurveyByTicketQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<CsatSurveyDto?> Handle(GetSurveyByTicketQuery request, CancellationToken cancellationToken)
    {
        return await _context.CsatSurveys
            .AsNoTracking()
            .Where(s => s.TicketId == request.TicketId)
            .Select(s => new CsatSurveyDto(
                s.Id, s.TicketId, s.CustomerId, s.Rating, s.Feedback,
                s.SentAt, s.ExpiresAt, s.SubmittedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
