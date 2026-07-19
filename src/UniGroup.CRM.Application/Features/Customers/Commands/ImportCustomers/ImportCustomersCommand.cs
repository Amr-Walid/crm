using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Common.Models;
using UniGroup.CRM.Domain.Entities;

namespace UniGroup.CRM.Application.Features.Customers.Commands.ImportCustomers;

/// <summary>
/// Command to bulk import customers from a set of parsed Excel rows.
/// FullName and PhoneNumber are required per row; rows whose phone number
/// already exists in the system are skipped to avoid unique-index violations.
/// </summary>
/// <param name="Rows">The parsed customer rows to import.</param>
public record ImportCustomersCommand(IReadOnlyList<CustomerImportRow> Rows) : IRequest<ImportCustomersResult>;

/// <summary>
/// Handler for <see cref="ImportCustomersCommand"/>. Validates each row,
/// enforces phone uniqueness (against both the database and within the batch),
/// and persists valid customers in a single transaction.
/// </summary>
public class ImportCustomersCommandHandler : IRequestHandler<ImportCustomersCommand, ImportCustomersResult>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportCustomersCommandHandler"/> class.
    /// </summary>
    public ImportCustomersCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<ImportCustomersResult> Handle(ImportCustomersCommand request, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var successCount = 0;

        if (request.Rows.Count == 0)
        {
            return new ImportCustomersResult(0, 0, new List<string> { "The uploaded sheet contained no data rows." });
        }

        // Normalize and collect candidate phone numbers so we can look them up
        // in one query instead of hitting the database per row.
        var candidatePhones = request.Rows
            .Select(r => r.PhoneNumber?.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .Distinct()
            .ToList();

        var existingPhones = candidatePhones.Count == 0
            ? new HashSet<string>()
            : (await _context.CustomerPhones
                    .AsNoTracking()
                    .Where(p => candidatePhones.Contains(p.Phone))
                    .Select(p => p.Phone)
                    .ToListAsync(cancellationToken))
                .ToHashSet();

        // Track phones added within this same batch to avoid intra-file duplicates.
        var seenInBatch = new HashSet<string>();

        var rowNumber = 1; // header is row 1; data starts at row 2
        foreach (var row in request.Rows)
        {
            rowNumber++;

            var fullName = row.FullName?.Trim();
            var phone = row.PhoneNumber?.Trim();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                errors.Add($"Row {rowNumber}: FullName is required — skipped.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(phone))
            {
                errors.Add($"Row {rowNumber}: PhoneNumber is required — skipped.");
                continue;
            }

            if (existingPhones.Contains(phone))
            {
                errors.Add($"Row {rowNumber}: Phone '{phone}' already exists — skipped.");
                continue;
            }

            if (!seenInBatch.Add(phone))
            {
                errors.Add($"Row {rowNumber}: Phone '{phone}' is duplicated within the file — skipped.");
                continue;
            }

            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                Name = fullName,
                Email = string.IsNullOrWhiteSpace(row.Email) ? null : row.Email.Trim(),
                Province = string.IsNullOrWhiteSpace(row.Province) ? null : row.Province.Trim(),
                City = string.IsNullOrWhiteSpace(row.City) ? null : row.City.Trim(),
                AddressDetails = string.IsNullOrWhiteSpace(row.AddressDetails) ? null : row.AddressDetails.Trim(),
                CustomerGroup = string.IsNullOrWhiteSpace(row.CustomerGroup) ? null : row.CustomerGroup.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            customer.CustomerPhones.Add(new CustomerPhone
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                Phone = phone,
                IsPrimary = true
            });

            _context.Customers.Add(customer);
            successCount++;
        }

        if (successCount > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new ImportCustomersResult(successCount, errors.Count, errors);
    }
}
