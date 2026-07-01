using MediatR;
using Microsoft.EntityFrameworkCore;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace UniGroup.CRM.Application.Features.Devices.Commands.AddCustomerDevice;

/// <summary>
/// Command to link a customer to a device with purchase and warranty information.
/// </summary>
public record AddCustomerDeviceCommand(
    Guid CustomerId,
    Guid ModelId,
    string? IMEI,
    string? SerialNumber,
    DateTime PurchaseDate,
    string? InvoiceNumber,
    DateTime? WarrantyExpiry
) : IRequest<Guid>;

/// <summary>
/// Handler for executing the add customer device command.
/// </summary>
public class AddCustomerDeviceCommandHandler : IRequestHandler<AddCustomerDeviceCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddCustomerDeviceCommandHandler"/> class.
    /// </summary>
    public AddCustomerDeviceCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Handles assigning a device to a customer.
    /// </summary>
    public async Task<Guid> Handle(AddCustomerDeviceCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify customer exists
        var customerExists = await _context.Customers
            .AnyAsync(c => c.Id == request.CustomerId, cancellationToken);

        if (!customerExists)
        {
            throw new Exception($"Customer with ID '{request.CustomerId}' does not exist.");
        }

        // 2. Verify model exists
        var modelExists = await _context.DeviceModels
            .AnyAsync(m => m.Id == request.ModelId, cancellationToken);

        if (!modelExists)
        {
            throw new Exception($"Device Model with ID '{request.ModelId}' does not exist.");
        }

        // 3. Verify IMEI is unique (if provided)
        if (!string.IsNullOrWhiteSpace(request.IMEI))
        {
            var imeiExists = await _context.CustomerDevices
                .AnyAsync(cd => cd.IMEI == request.IMEI, cancellationToken);

            if (imeiExists)
            {
                throw new Exception($"Device with IMEI '{request.IMEI}' is already registered.");
            }
        }

        // 4. Verify SerialNumber is unique (if provided)
        if (!string.IsNullOrWhiteSpace(request.SerialNumber))
        {
            var snExists = await _context.CustomerDevices
                .AnyAsync(cd => cd.SerialNumber == request.SerialNumber, cancellationToken);

            if (snExists)
            {
                throw new Exception($"Device with Serial Number '{request.SerialNumber}' is already registered.");
            }
        }

        // Calculate warranty expiry date if not provided (defaulting to Purchase Date + 2 years)
        var warrantyExpiry = request.WarrantyExpiry ?? request.PurchaseDate.AddYears(2);

        var customerDevice = new CustomerDevice
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            ModelId = request.ModelId,
            IMEI = request.IMEI,
            SerialNumber = request.SerialNumber,
            PurchaseDate = request.PurchaseDate,
            InvoiceNumber = request.InvoiceNumber,
            WarrantyExpiry = warrantyExpiry
        };

        _context.CustomerDevices.Add(customerDevice);
        await _context.SaveChangesAsync(cancellationToken);

        return customerDevice.Id;
    }
}
