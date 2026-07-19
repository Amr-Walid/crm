using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.Customers.Commands.CreateCustomer;
using UniGroup.CRM.Application.Features.Customers.Commands.ImportCustomers;
using UniGroup.CRM.Application.Features.Customers.Queries.Common;
using UniGroup.CRM.Application.Features.Customers.Queries.GetCustomerDetails;
using UniGroup.CRM.Application.Features.Customers.Queries.SearchCustomers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UniGroup.CRM.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IExcelCustomerImportService _excelImport;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomersController"/> class.
    /// </summary>
    public CustomersController(ISender sender, IExcelCustomerImportService excelImport)
    {
        _sender = sender;
        _excelImport = excelImport;
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
                request.Phone,
                request.CustomerGroup
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

    /// <summary>
    /// Downloads the blank Excel (.xlsx) template used for bulk customer import.
    /// The template contains a single header row with the expected columns.
    /// </summary>
    [HttpGet("import/template")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult DownloadImportTemplate()
    {
        var bytes = _excelImport.GenerateTemplate();
        const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        return File(bytes, contentType, "customers_import_template.xlsx");
    }

    /// <summary>
    /// Imports customers in bulk from an uploaded Excel (.xlsx) file.
    /// FullName and PhoneNumber are required per row; duplicate phone numbers are skipped.
    /// </summary>
    /// <param name="file">The uploaded .xlsx file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [HttpPost("import")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB safety cap
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file was uploaded." });
            }

            var extension = Path.GetExtension(file.FileName);
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Only .xlsx files are supported." });
            }

            List<UniGroup.CRM.Application.Common.Models.CustomerImportRow> rows;
            await using (var stream = file.OpenReadStream())
            {
                // Buffer into memory so MiniExcel can seek freely on the stream.
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, cancellationToken);
                ms.Position = 0;
                rows = _excelImport.ParseRows(ms);
            }

            var command = new ImportCustomersCommand(rows);
            var result = await _sender.Send(command, cancellationToken);
            return Ok(result);
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
    string Phone,
    string? CustomerGroup = null
);
