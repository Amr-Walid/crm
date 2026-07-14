using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Features.Csat.Commands.SubmitCsatSurvey;
using UniGroup.CRM.Application.Features.Csat.Queries.GetCsatReport;
using UniGroup.CRM.Application.Features.Csat.Queries.GetSurveyByTicket;

namespace UniGroup.CRM.API.Controllers;

/// <summary>
/// Request body for anonymous CSAT survey submission.
/// </summary>
/// <param name="Token">The opaque survey token from the survey link.</param>
/// <param name="Rating">Rating from 1 to 5.</param>
/// <param name="Feedback">Optional free-text feedback.</param>
public record SubmitSurveyRequest(string Token, int Rating, string? Feedback);

/// <summary>
/// Controller for CSAT survey submission (anonymous, token-secured) and reporting.
/// </summary>
[ApiController]
[Route("api/surveys")]
public class CsatController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsatController"/> class.
    /// </summary>
    public CsatController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Submits a CSAT survey response. Anonymous — secured by the opaque survey token.
    /// Rejects expired tokens (7 days), invalid tokens, and repeat submissions.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("submit")]
    [ProducesResponseType(typeof(SubmitCsatSurveyResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SubmitCsatSurveyResult), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitSurveyRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new SubmitCsatSurveyCommand(request.Token, request.Rating, request.Feedback),
            cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Gets the aggregated CSAT report (response rate, average rating, distribution).
    /// </summary>
    [Authorize(Roles = "Admin,Team Leader,TeamLeader")]
    [HttpGet("report")]
    [ProducesResponseType(typeof(CsatReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReport(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        var report = await _sender.Send(new GetCsatReportQuery(from, to), cancellationToken);
        return Ok(report);
    }

    /// <summary>
    /// Gets the CSAT survey linked to a specific ticket.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("ticket/{ticketId}")]
    [ProducesResponseType(typeof(CsatSurveyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByTicket(string ticketId, CancellationToken cancellationToken)
    {
        var survey = await _sender.Send(new GetSurveyByTicketQuery(ticketId), cancellationToken);
        return survey == null ? NotFound() : Ok(survey);
    }
}
