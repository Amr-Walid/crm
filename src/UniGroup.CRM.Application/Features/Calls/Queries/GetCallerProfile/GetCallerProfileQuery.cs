using MediatR;
using Microsoft.EntityFrameworkCore;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.Customers.Queries.Common;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using UniGroup.CRM.Domain.Entities;

namespace UniGroup.CRM.Application.Features.Calls.Queries.GetCallerProfile;

/// <summary>
/// Query to retrieve the full customer profile associated with a given phone number.
/// This powers the real-time Caller ID feature: when a call comes in, the system
/// instantly identifies the customer and returns their 360° view.
/// </summary>
/// <param name="PhoneNumber">The inbound caller's phone number to look up.</param>
public record GetCallerProfileQuery(string PhoneNumber) : IRequest<CustomerDetailsDto?>;

/// <summary>
/// Handler for <see cref="GetCallerProfileQuery"/>.
/// Searches the <c>CustomerPhones</c> table by phone number. If a match is found,
/// returns the complete customer profile; otherwise returns null (unknown caller).
/// </summary>
public class GetCallerProfileQueryHandler : IRequestHandler<GetCallerProfileQuery, CustomerDetailsDto?>
{
    private readonly IApplicationDbContext _context;

    private static readonly Func<DbContext, string, Task<Customer?>> _compiledCallerQuery =
        EF.CompileAsyncQuery((DbContext context, string phone) =>
            context.Set<Customer>()
                .AsNoTracking()
                .Include(c => c.CustomerPhones)
                .Include(c => c.CustomerDevices)
                    .ThenInclude(d => d.Model)
                        .ThenInclude(m => m.Brand)
                .FirstOrDefault(c => c.CustomerPhones.Any(p => p.Phone == phone)));

    /// <summary>
    /// Initializes a new instance of the <see cref="GetCallerProfileQueryHandler"/> class.
    /// </summary>
    /// <param name="context">The application database context.</param>
    public GetCallerProfileQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Handles the caller profile lookup.
    /// Single database query: joins CustomerPhones → Customer → CustomerDevices → Model → Brand.
    /// Returns null (not an exception) when the phone number is not registered.
    /// </summary>
    /// <param name="request">The query containing the phone number to search.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A <see cref="CustomerDetailsDto"/> if the phone number belongs to a known customer;
    /// otherwise null to indicate an unregistered caller.
    /// </returns>
    public async Task<CustomerDetailsDto?> Handle(
        GetCallerProfileQuery request,
        CancellationToken cancellationToken)
    {
        var normalizedPhone = request.PhoneNumber?.Trim() ?? string.Empty;

        // Execute via EF Core Compiled Query for high performance
        var dbContext = (DbContext)_context;
        var customer = await _compiledCallerQuery(dbContext, normalizedPhone);

        if (customer == null)
        {
            // Unknown caller – return null so the UI can open a new-customer form
            return null;
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
