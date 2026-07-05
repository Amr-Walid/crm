using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Features.Dashboards.Queries.Common;
using UniGroup.CRM.Application.Features.Dashboards.Queries.GetAgentPerformance;
using UniGroup.CRM.Application.Features.Dashboards.Queries.GetDashboardSummary;
using UniGroup.CRM.Application.Features.Dashboards.Queries.GetDeviceFailureReport;
using UniGroup.CRM.Application.Features.Dashboards.Queries.GetHourlyCallVolume;
using UniGroup.CRM.Application.Features.Dashboards.Queries.GetTicketsByStatus;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.API.Controllers;

/// <summary>
/// Controller for dashboard statistics and real-time operational indicators.
/// </summary>
[Authorize(Roles = "Admin,Team Leader,TeamLeader")]
[ApiController]
[Route("api/dashboard")]
public class DashboardsController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardsController"/> class.
    /// </summary>
    public DashboardsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Gets real-time dashboard summary metrics.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DashboardSummaryDto))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSummary([FromQuery] DateTime? date, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new GetDashboardSummaryQuery(date), cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Gets performance statistics for agents.
    /// </summary>
    [HttpGet("agent-performance")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<AgentPerformanceDto>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAgentPerformance(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? agentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new GetAgentPerformanceQuery(dateFrom, dateTo, agentId), cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Gets device failure analysis report metrics.
    /// </summary>
    [HttpGet("device-failures")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<DeviceFailureReportDto>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDeviceFailures(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? brandId,
        [FromQuery] Guid? modelId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new GetDeviceFailureReportQuery(dateFrom, dateTo, brandId, modelId), cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Gets hourly call volume statistics for a specific day.
    /// </summary>
    [HttpGet("call-volume")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<HourlyCallVolumeDto>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCallVolume([FromQuery] DateTime? date, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new GetHourlyCallVolumeQuery(date), cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Gets ticket count distribution by status.
    /// </summary>
    [HttpGet("tickets-by-status")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Dictionary<TicketStatus, int>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTicketsByStatus(
        [FromQuery] Guid? departmentId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new GetTicketsByStatusQuery(departmentId, dateFrom, dateTo), cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
