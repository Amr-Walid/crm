using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UniGroup.CRM.Application.Features.Calls.Commands.LogCall;
using UniGroup.CRM.Application.Features.Calls.Queries.Common;
using UniGroup.CRM.Application.Features.Calls.Queries.GetCallerProfile;
using UniGroup.CRM.Application.Features.Calls.Queries.GetCallHistory;
using UniGroup.CRM.Application.Features.Customers.Queries.Common;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace UniGroup.CRM.API.Controllers;

/// <summary>
/// API controller for call center operations including logging calls,
/// Caller ID lookup, and retrieving customer call history.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CallsController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallsController"/> class.
    /// </summary>
    /// <param name="sender">The MediatR sender for dispatching commands and queries.</param>
    public CallsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Logs a completed call record into the CRM system.
    /// </summary>
    /// <param name="request">The call details to persist.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The ID of the newly created call record.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Guid))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LogCall(
        [FromBody] LogCallRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // AgentId is resolved from the authenticated JWT 'sub' claim
            // to prevent identity spoofing via request body manipulation.
            var agentIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (!Guid.TryParse(agentIdClaim, out var agentId))
            {
                return Unauthorized(new { message = "Invalid agent identity in token." });
            }

            var command = new LogCallCommand(
                request.CustomerId,
                agentId,
                request.Direction,
                request.PhoneNumber,
                request.DurationSeconds,
                request.Summary,
                request.RecordingUrl
            );

            var callId = await _sender.Send(command, cancellationToken);

            // When customerId is null (unknown caller), CreatedAtAction would fail
            // because the history route requires a non-null Guid. Return Created with
            // a relative URI instead to maintain RFC 9110 semantics.
            if (request.CustomerId.HasValue)
            {
                return CreatedAtAction(
                    nameof(GetCallHistory),
                    new { customerId = request.CustomerId.Value },
                    callId);
            }

            return Created($"/api/calls/{callId}", callId);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Caller ID lookup endpoint. Given a phone number, returns the matching customer's
    /// full 360° profile for instant identification when a call is received.
    /// Returns null if the caller is not registered in the system.
    /// </summary>
    /// <param name="phoneNumber">The inbound caller's phone number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Full <see cref="CustomerDetailsDto"/> if found; otherwise a 200 OK with null body.</returns>
    [HttpGet("caller-id")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CustomerDetailsDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCallerProfile(
        [FromQuery] string phoneNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return BadRequest(new { message = "Phone number is required." });
            }

            var query = new GetCallerProfileQuery(phoneNumber);
            var result = await _sender.Send(query, cancellationToken);

            // Return 200 OK with null body when caller is unknown — the UI uses this
            // to decide whether to show an existing customer card or a new-customer form.
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Retrieves the paged call history for a given customer.
    /// </summary>
    /// <param name="customerId">The customer whose call history is requested.</param>
    /// <param name="page">The 1-based page number (defaults to 1).</param>
    /// <param name="pageSize">The number of records per page (defaults to 20, max 100).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A paged list of <see cref="CallDto"/> ordered newest first.</returns>
    [HttpGet("history/{customerId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<CallDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCallHistory(
        [FromRoute] Guid customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new GetCallHistoryQuery(customerId, page, pageSize);
            var result = await _sender.Send(query, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

/// <summary>
/// Request DTO for logging a call via the API.
/// AgentId is intentionally excluded – it is resolved from the JWT claims by the controller.
/// </summary>
public record LogCallRequest(
    Guid? CustomerId,
    string Direction,
    string PhoneNumber,
    int DurationSeconds,
    string? Summary,
    string? RecordingUrl
);
