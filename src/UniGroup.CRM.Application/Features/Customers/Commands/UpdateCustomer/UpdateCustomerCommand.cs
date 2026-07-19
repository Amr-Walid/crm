using MediatR;
using Microsoft.EntityFrameworkCore;
using UniGroup.CRM.Application.Common.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace UniGroup.CRM.Application.Features.Customers.Commands.UpdateCustomer;

/// <summary>
/// Command to update an existing customer's profile details ("استكمال البيانات").
/// The primary phone number is intentionally not editable through this command.
/// </summary>
/// <param name="Id">The unique identifier of the customer to update.</param>
/// <param name="Name">The customer's full name (required).</param>
/// <param name="Email">The optional email address.</param>
/// <param name="Province">The optional province / governorate.</param>
/// <param name="City">The optional city.</param>
/// <param name="AddressDetails">The optional detailed address.</param>
/// <param name="CustomerGroup">The optional customer group / segment.</param>
public record UpdateCustomerCommand(
    Guid Id,
    string Name,
    string? Email,
    string? Province,
    string? City,
    string? AddressDetails,
    string? CustomerGroup
) : IRequest;

/// <summary>
/// Handler for executing the update customer command.
/// </summary>
public class UpdateCustomerCommandHandler : IRequestHandler<UpdateCustomerCommand>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateCustomerCommandHandler"/> class.
    /// </summary>
    public UpdateCustomerCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Handles the customer update by locating the customer and persisting the new values.
    /// </summary>
    /// <exception cref="Exception">Thrown when the customer does not exist.</exception>
    public async Task Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (customer is null)
        {
            throw new Exception($"Customer with ID {request.Id} does not exist.");
        }

        customer.Name = request.Name.Trim();
        customer.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        customer.Province = string.IsNullOrWhiteSpace(request.Province) ? null : request.Province.Trim();
        customer.City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();
        customer.AddressDetails = string.IsNullOrWhiteSpace(request.AddressDetails) ? null : request.AddressDetails.Trim();
        customer.CustomerGroup = string.IsNullOrWhiteSpace(request.CustomerGroup) ? null : request.CustomerGroup.Trim();

        await _context.SaveChangesAsync(cancellationToken);
    }
}
