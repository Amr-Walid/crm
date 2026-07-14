using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Features.Notifications.Queries.GetNotificationLogs;

namespace UniGroup.CRM.API.Controllers;

/// <summary>
/// Controller exposing the notification dispatch log. Admin only.
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationsController"/> class.
    /// </summary>
    public NotificationsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Gets a paged, filterable list of dispatched notifications (newest first).
    /// </summary>
    [HttpGet("logs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? channel,
        [FromQuery] string? status,
        [FromQuery] string? templateType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetNotificationLogsQuery(channel, status, templateType, page, pageSize),
            cancellationToken);

        return Ok(result);
    }
}
