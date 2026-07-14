using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;

namespace UniGroup.CRM.Application.Features.Csat.Commands.SubmitCsatSurvey;

/// <summary>
/// Result of a CSAT survey submission attempt.
/// </summary>
/// <param name="Success">Whether the submission was accepted.</param>
/// <param name="Message">Human-readable outcome message.</param>
public record SubmitCsatSurveyResult(bool Success, string Message);

/// <summary>
/// Anonymous command to submit a CSAT survey response using an opaque token.
/// Enforces token validity, 7-day expiration, and single submission.
/// </summary>
/// <param name="Token">The opaque survey token sent to the customer.</param>
/// <param name="Rating">Rating from 1 to 5.</param>
/// <param name="Feedback">Optional free-text feedback.</param>
public record SubmitCsatSurveyCommand(string Token, int Rating, string? Feedback)
    : IRequest<SubmitCsatSurveyResult>;

/// <summary>
/// Handler validating the token and recording the survey response.
/// </summary>
public class SubmitCsatSurveyCommandHandler
    : IRequestHandler<SubmitCsatSurveyCommand, SubmitCsatSurveyResult>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubmitCsatSurveyCommandHandler"/> class.
    /// </summary>
    public SubmitCsatSurveyCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<SubmitCsatSurveyResult> Handle(
        SubmitCsatSurveyCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return new SubmitCsatSurveyResult(false, "Survey token is required.");
        }

        if (request.Rating < 1 || request.Rating > 5)
        {
            return new SubmitCsatSurveyResult(false, "Rating must be between 1 and 5.");
        }

        var survey = await _context.CsatSurveys
            .FirstOrDefaultAsync(s => s.SurveyToken == request.Token, cancellationToken);

        if (survey == null)
        {
            return new SubmitCsatSurveyResult(false, "Invalid survey token.");
        }

        if (survey.SubmittedAt.HasValue)
        {
            return new SubmitCsatSurveyResult(false, "This survey has already been submitted.");
        }

        if (DateTime.UtcNow > survey.ExpiresAt)
        {
            return new SubmitCsatSurveyResult(false, "This survey link has expired.");
        }

        survey.Rating = request.Rating;
        survey.Feedback = request.Feedback;
        survey.SubmittedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return new SubmitCsatSurveyResult(true, "Thank you for your feedback!");
    }
}
