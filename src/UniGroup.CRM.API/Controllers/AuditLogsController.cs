using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Features.AuditLogs.Queries.GetAuditLogDetails;
using UniGroup.CRM.Application.Features.AuditLogs.Queries.GetAuditLogs;

namespace UniGroup.CRM.API.Controllers;

/// <summary>
/// Controller exposing the system audit trail. Admin only.
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/audit-logs")]
public class AuditLogsController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogsController"/> class.
    /// </summary>
    public AuditLogsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Gets a paged, filterable list of audit entries (newest first).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? tableName,
        [FromQuery] string? action,
        [FromQuery] Guid? userId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetAuditLogsQuery(tableName, action, userId, from, to, page, pageSize),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Gets a single audit entry including before/after JSON payloads.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AuditLogDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuditLogDetails(Guid id, CancellationToken cancellationToken)
    {
        var details = await _sender.Send(new GetAuditLogDetailsQuery(id), cancellationToken);
        return details == null ? NotFound() : Ok(details);
    }
}
