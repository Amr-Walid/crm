using MediatR;
using Microsoft.EntityFrameworkCore;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.Customers.Queries.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UniGroup.CRM.Application.Features.Customers.Queries.SearchCustomers;

/// <summary>
/// Query to search customers by Name, Phone, SerialNumber, or IMEI.
/// </summary>
public record SearchCustomersQuery(string SearchTerm) : IRequest<List<CustomerDetailsDto>>;

/// <summary>
/// Handler for executing the search customers query.
/// </summary>
public class SearchCustomersQueryHandler : IRequestHandler<SearchCustomersQuery, List<CustomerDetailsDto>>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchCustomersQueryHandler"/> class.
    /// </summary>
    public SearchCustomersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Handles searching customers.
    /// </summary>
    public async Task<List<CustomerDetailsDto>> Handle(SearchCustomersQuery request, CancellationToken cancellationToken)
    {
        var term = request.SearchTerm?.Trim().ToLower() ?? string.Empty;

        var query = _context.Customers
            .AsNoTracking()
            .Include(c => c.CustomerPhones)
            .Include(c => c.CustomerDevices)
                .ThenInclude(d => d.Model)
                    .ThenInclude(m => m.Brand)
            .AsQueryable();

        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(c => 
                c.Name.ToLower().Contains(term) ||
                (c.Email != null && c.Email.ToLower().Contains(term)) ||
                c.CustomerPhones.Any(p => p.Phone.Contains(term)) ||
                c.CustomerDevices.Any(d => d.IMEI != null && d.IMEI.Contains(term)) ||
                c.CustomerDevices.Any(d => d.SerialNumber != null && d.SerialNumber.Contains(term))
            );
        }

        var customers = await query.ToListAsync(cancellationToken);
        var currentDate = DateTime.UtcNow;

        return customers.Select(customer =>
        {
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
                devices,
                customer.CustomerGroup
            );
        }).ToList();
    }
}
