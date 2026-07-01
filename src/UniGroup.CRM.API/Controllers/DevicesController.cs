using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UniGroup.CRM.Application.Features.Devices.Commands.AddCustomerDevice;
using UniGroup.CRM.Application.Features.Devices.Commands.CreateDeviceBrand;
using UniGroup.CRM.Application.Features.Devices.Commands.CreateDeviceModel;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace UniGroup.CRM.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevicesController"/> class.
    /// </summary>
    public DevicesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Creates a new device brand.
    /// </summary>
    [HttpPost("brands")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Guid))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateBrand([FromBody] CreateBrandRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var command = new CreateDeviceBrandCommand(request.Name);
            var brandId = await _sender.Send(command, cancellationToken);
            return Ok(brandId);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Creates a new device model under a brand.
    /// </summary>
    [HttpPost("models")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Guid))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateModel([FromBody] CreateModelRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var command = new CreateDeviceModelCommand(request.BrandId, request.Name);
            var modelId = await _sender.Send(command, cancellationToken);
            return Ok(modelId);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Assigns a device to a customer with purchase and warranty details.
    /// </summary>
    [HttpPost("assign")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Guid))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignDevice([FromBody] AssignDeviceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var command = new AddCustomerDeviceCommand(
                request.CustomerId,
                request.ModelId,
                request.IMEI,
                request.SerialNumber,
                request.PurchaseDate,
                request.InvoiceNumber,
                request.WarrantyExpiry
            );

            var customerDeviceId = await _sender.Send(command, cancellationToken);
            return Ok(customerDeviceId);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

/// <summary>
/// Request DTO for creating a brand.
/// </summary>
public record CreateBrandRequest(string Name);

/// <summary>
/// Request DTO for creating a device model.
/// </summary>
public record CreateModelRequest(Guid BrandId, string Name);

/// <summary>
/// Request DTO for assigning a device to a customer.
/// </summary>
public record AssignDeviceRequest(
    Guid CustomerId,
    Guid ModelId,
    string? IMEI,
    string? SerialNumber,
    DateTime PurchaseDate,
    string? InvoiceNumber,
    DateTime? WarrantyExpiry
);
