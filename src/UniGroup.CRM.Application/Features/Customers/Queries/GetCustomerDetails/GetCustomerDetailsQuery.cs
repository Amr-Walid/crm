using MediatR;
using Microsoft.EntityFrameworkCore;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.Customers.Queries.Common;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using UniGroup.CRM.Domain.Entities;

namespace UniGroup.CRM.Application.Features.Customers.Queries.GetCustomerDetails;

/// <summary>
/// Query to fetch full customer details, including phone numbers, devices, and warranty status.
/// </summary>
public record GetCustomerDetailsQuery(Guid Id) : IRequest<CustomerDetailsDto>;

/// <summary>
/// Handler for executing the get customer details query.
/// </summary>
public class GetCustomerDetailsQueryHandler : IRequestHandler<GetCustomerDetailsQuery, CustomerDetailsDto>
{
    private readonly IApplicationDbContext _context;

    private static readonly Func<DbContext, Guid, Task<Customer?>> _compiledCustomerQuery =
        EF.CompileAsyncQuery((DbContext context, Guid id) =>
            context.Set<Customer>()
                .AsNoTracking()
                .Include(c => c.CustomerPhones)
                .Include(c => c.CustomerDevices)
                    .ThenInclude(d => d.Model)
                        .ThenInclude(m => m.Brand)
                .FirstOrDefault(c => c.Id == id));

    /// <summary>
    /// Initializes a new instance of the <see cref="GetCustomerDetailsQueryHandler"/> class.
    /// </summary>
    public GetCustomerDetailsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Handles fetching customer details.
    /// </summary>
    public async Task<CustomerDetailsDto> Handle(GetCustomerDetailsQuery request, CancellationToken cancellationToken)
    {
        var dbContext = (DbContext)_context;
        var customer = await _compiledCustomerQuery(dbContext, request.Id);

        if (customer == null)
        {
            throw new Exception($"Customer with ID '{request.Id}' was not found.");
        }

        var currentDate = DateTime.UtcNow;

        var phones = customer.CustomerPhones
            .Select(p => new CustomerPhoneDto(p.Id, p.Phone, p.IsPrimary))
            .ToList();

        var devices = customer.CustomerDevices
            .Select(d => new CustomerDeviceDto(
                d.Id,
                d.ModelId,
                d.Model.Name,
                d.Model.Brand.Name,
                d.IMEI,
                d.SerialNumber,
                d.PurchaseDate,
                d.InvoiceNumber,
                d.WarrantyExpiry,
                d.WarrantyExpiry > currentDate ? "Active" : "Expired"
            ))
            .ToList();

        return new CustomerDetailsDto(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.Province,
            customer.City,
            customer.AddressDetails,
            customer.CreatedAt,
            phones,
            devices
        );
    }
}
