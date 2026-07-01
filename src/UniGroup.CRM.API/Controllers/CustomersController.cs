using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UniGroup.CRM.Application.Features.Customers.Commands.CreateCustomer;
using UniGroup.CRM.Application.Features.Customers.Queries.Common;
using UniGroup.CRM.Application.Features.Customers.Queries.GetCustomerDetails;
using UniGroup.CRM.Application.Features.Customers.Queries.SearchCustomers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UniGroup.CRM.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomersController"/> class.
    /// </summary>
    public CustomersController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Creates a new customer with a primary phone number.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Guid))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var command = new CreateCustomerCommand(
                request.Name,
                request.Email,
                request.Province,
                request.City,
                request.AddressDetails,
                request.Phone
            );

            var customerId = await _sender.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = customerId }, customerId);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Gets customer details by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CustomerDetailsDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var query = new GetCustomerDetailsQuery(id);
            var response = await _sender.Send(query, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Searches customers by Name, Phone, SerialNumber, or IMEI.
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<CustomerDetailsDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromQuery] string searchTerm, CancellationToken cancellationToken)
    {
        try
        {
            var query = new SearchCustomersQuery(searchTerm);
            var response = await _sender.Send(query, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

/// <summary>
/// Request DTO for creating a new customer.
/// </summary>
public record CreateCustomerRequest(
    string Name,
    string? Email,
    string? Province,
    string? City,
    string? AddressDetails,
    string Phone
);
