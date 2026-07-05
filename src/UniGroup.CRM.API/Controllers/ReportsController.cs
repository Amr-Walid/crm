using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Features.Reports.Queries.ExportAgentReport;

namespace UniGroup.CRM.API.Controllers;

/// <summary>
/// Controller for generating and exporting performance and operational reports.
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportsController"/> class.
    /// </summary>
    public ReportsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Exports the agent performance report as a CSV file.
    /// </summary>
    [HttpGet("agents/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportAgents(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new ExportAgentReportQuery(dateFrom, dateTo), cancellationToken);
            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
