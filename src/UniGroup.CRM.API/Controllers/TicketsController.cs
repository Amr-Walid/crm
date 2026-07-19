using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Features.Tickets.Commands.AddAttachment;
using UniGroup.CRM.Application.Features.Tickets.Commands.AddInternalNote;
using UniGroup.CRM.Application.Features.Tickets.Commands.AssignTicket;
using UniGroup.CRM.Application.Features.Tickets.Commands.CreateTicket;
using UniGroup.CRM.Application.Features.Tickets.Commands.TransitionTicketStatus;
using UniGroup.CRM.Application.Features.Tickets.Queries.Common;
using UniGroup.CRM.Application.Features.Tickets.Queries.GetMyTickets;
using UniGroup.CRM.Application.Features.Tickets.Queries.GetTicketDetails;
using UniGroup.CRM.Application.Features.Tickets.Queries.GetTicketsList;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketsController"/> class.
    /// </summary>
    public TicketsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Creates a new support ticket.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(string))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTicketRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var agentIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (!Guid.TryParse(agentIdClaim, out var agentId))
            {
                return Unauthorized(new { message = "Invalid agent identity in token." });
            }

            var command = new CreateTicketCommand(
                request.CustomerId,
                request.CustomerDeviceId,
                request.Title,
                request.Description,
                request.MainCategory,
                request.Category,
                request.Priority,
                agentId
            );

            var ticketId = await _sender.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { ticketId }, ticketId);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Gets ticket details by ticket ID.
    /// </summary>
    [HttpGet("{ticketId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TicketDetailsDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] string ticketId, CancellationToken cancellationToken)
    {
        try
        {
            var query = new GetTicketDetailsQuery(ticketId);
            var result = await _sender.Send(query, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Gets a filtered, paged list of tickets.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<TicketSummaryDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetList(
        [FromQuery] TicketStatus? status,
        [FromQuery] TicketPriority? priority,
        [FromQuery] Guid? departmentId,
        [FromQuery] Guid? assignedToId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new GetTicketsListQuery(status, priority, departmentId, assignedToId, dateFrom, dateTo, page, pageSize);
            var result = await _sender.Send(query, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Gets tickets assigned to the currently authenticated agent.
    /// </summary>
    [HttpGet("my")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<TicketSummaryDto>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyTickets([FromQuery] TicketStatus? status, CancellationToken cancellationToken)
    {
        try
        {
            var agentIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (!Guid.TryParse(agentIdClaim, out var agentId))
            {
                return Unauthorized(new { message = "Invalid agent identity in token." });
            }

            var query = new GetMyTicketsQuery(agentId, status);
            var result = await _sender.Send(query, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Transitions the status of a ticket.
    /// </summary>
    [HttpPatch("{ticketId}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TransitionStatus([FromRoute] string ticketId, [FromBody] TransitionStatusRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var agentIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (!Guid.TryParse(agentIdClaim, out var agentId))
            {
                return Unauthorized(new { message = "Invalid agent identity in token." });
            }

            var command = new TransitionTicketStatusCommand(ticketId, request.NewStatus, agentId, request.Note);
            await _sender.Send(command, cancellationToken);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Assigns a ticket to an agent and/or department.
    /// </summary>
    [HttpPatch("{ticketId}/assign")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Assign([FromRoute] string ticketId, [FromBody] AssignTicketRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var agentIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (!Guid.TryParse(agentIdClaim, out var agentId))
            {
                return Unauthorized(new { message = "Invalid agent identity in token." });
            }

            var command = new AssignTicketCommand(ticketId, request.AssignedToId, request.DepartmentId, agentId, request.Note);
            await _sender.Send(command, cancellationToken);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Adds an internal note to a ticket.
    /// </summary>
    [HttpPost("{ticketId}/notes")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Guid))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddInternalNote([FromRoute] string ticketId, [FromBody] AddInternalNoteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var agentIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (!Guid.TryParse(agentIdClaim, out var agentId))
            {
                return Unauthorized(new { message = "Invalid agent identity in token." });
            }

            var command = new AddInternalNoteCommand(ticketId, request.Content, agentId);
            var noteId = await _sender.Send(command, cancellationToken);
            return Ok(noteId);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Uploads and attaches a file to a ticket.
    /// </summary>
    [HttpPost("{ticketId}/attachments")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Guid))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddAttachment([FromRoute] string ticketId, IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file was provided or file is empty." });
            }

            var agentIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (!Guid.TryParse(agentIdClaim, out var agentId))
            {
                return Unauthorized(new { message = "Invalid agent identity in token." });
            }

            using var stream = file.OpenReadStream();
            var command = new AddAttachmentCommand(
                ticketId,
                file.FileName,
                file.ContentType,
                stream,
                file.Length,
                agentId
            );

            var attachmentId = await _sender.Send(command, cancellationToken);
            return Ok(attachmentId);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public record CreateTicketRequest(
    Guid CustomerId,
    Guid? CustomerDeviceId,
    string Title,
    string Description,
    MainCategory MainCategory,
    TicketCategory Category,
    TicketPriority Priority
);

public record TransitionStatusRequest(
    TicketStatus NewStatus,
    string? Note
);

public record AssignTicketRequest(
    Guid? AssignedToId,
    Guid? DepartmentId,
    string? Note
);

public record AddInternalNoteRequest(
    string Content
);
