using MediatR;
using Microsoft.EntityFrameworkCore;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.Customers.Queries.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UniGroup.CRM.Application.Features.Calls.Queries.SearchSystem;

/// <summary>
/// Query for unified cross-entity search across the entire CRM system.
/// Searches customers by Name and Email, phone numbers, and device identifiers (IMEI, SerialNumber).
/// </summary>
/// <param name="SearchTerm">The term to search for across all indexed fields.</param>
public record SearchSystemQuery(string SearchTerm) : IRequest<List<CustomerDetailsDto>>;

/// <summary>
/// Handler for <see cref="SearchSystemQuery"/>.
/// Executes a single EF Core query with OR predicates across Customers, CustomerPhones,
/// and CustomerDevices tables. All searched columns have database indexes for performance.
/// </summary>
public class SearchSystemQueryHandler : IRequestHandler<SearchSystemQuery, List<CustomerDetailsDto>>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchSystemQueryHandler"/> class.
    /// </summary>
    /// <param name="context">The application database context.</param>
    public SearchSystemQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Handles the unified system search.
    /// </summary>
    /// <param name="request">The query containing the search term.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A list of <see cref="CustomerDetailsDto"/> matching the search term,
    /// or an empty list if no results are found.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when the search term is null or whitespace.</exception>
    public async Task<List<CustomerDetailsDto>> Handle(
        SearchSystemQuery request,
        CancellationToken cancellationToken)
    {
        var term = request.SearchTerm?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(term))
        {
            throw new ArgumentException("Search term cannot be empty.", nameof(request.SearchTerm));
        }

        // Single query with OR conditions across all indexed search fields
        var customers = await _context.Customers
            .AsNoTracking()
            .Include(c => c.CustomerPhones)
            .Include(c => c.CustomerDevices)
                .ThenInclude(d => d.Model)
                    .ThenInclude(m => m.Brand)
            .Where(c =>
                c.Name.Contains(term) ||
                (c.Email != null && c.Email.Contains(term)) ||
                c.CustomerPhones.Any(p => p.Phone.Contains(term)) ||
                c.CustomerDevices.Any(d => d.IMEI != null && d.IMEI.Contains(term)) ||
                c.CustomerDevices.Any(d => d.SerialNumber != null && d.SerialNumber.Contains(term))
            )
            .ToListAsync(cancellationToken);

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
                devices
            );
        }).ToList();
    }
}
