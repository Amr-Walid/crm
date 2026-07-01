using MediatR;
using Microsoft.EntityFrameworkCore;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace UniGroup.CRM.Application.Features.Customers.Commands.CreateCustomer;

/// <summary>
/// Command to create a new customer with a primary phone number.
/// </summary>
public record CreateCustomerCommand(
    string Name,
    string? Email,
    string? Province,
    string? City,
    string? AddressDetails,
    string Phone
) : IRequest<Guid>;

/// <summary>
/// Handler for executing the create customer command.
/// </summary>
public class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateCustomerCommandHandler"/> class.
    /// </summary>
    public CreateCustomerCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Handles customer creation.
    /// </summary>
    public async Task<Guid> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        // Check if primary phone number already exists
        var phoneExists = await _context.CustomerPhones
            .AnyAsync(p => p.Phone == request.Phone, cancellationToken);

        if (phoneExists)
        {
            throw new Exception($"Phone number '{request.Phone}' is already registered to another customer.");
        }

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            Province = request.Province,
            City = request.City,
            AddressDetails = request.AddressDetails,
            CreatedAt = DateTime.UtcNow
        };

        var customerPhone = new CustomerPhone
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Phone = request.Phone,
            IsPrimary = true
        };

        customer.CustomerPhones.Add(customerPhone);

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(cancellationToken);

        return customer.Id;
    }
}
