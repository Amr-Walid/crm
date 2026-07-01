using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UniGroup.CRM.Application.Features.Calls.Queries.SearchSystem;
using UniGroup.CRM.Application.Features.Customers.Queries.Common;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UniGroup.CRM.API.Controllers;

/// <summary>
/// API controller providing a unified search endpoint across all CRM entities.
/// Searches customers by Name, Email, Phone, IMEI, and SerialNumber in a single call.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchController"/> class.
    /// </summary>
    /// <param name="sender">The MediatR sender for dispatching queries.</param>
    public SearchController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Performs a unified search across the entire CRM system.
    /// The query is matched against customer names, emails, phone numbers,
    /// device IMEIs, and serial numbers simultaneously.
    /// </summary>
    /// <param name="q">The search term (minimum 1 character).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of matching <see cref="CustomerDetailsDto"/> records.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<CustomerDetailsDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest(new { message = "Search term 'q' is required." });
            }

            var query = new SearchSystemQuery(q);
            var results = await _sender.Send(query, cancellationToken);
            return Ok(results);
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
}
