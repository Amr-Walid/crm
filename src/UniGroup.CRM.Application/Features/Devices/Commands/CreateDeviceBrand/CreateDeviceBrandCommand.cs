using MediatR;
using Microsoft.EntityFrameworkCore;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace UniGroup.CRM.Application.Features.Devices.Commands.CreateDeviceBrand;

/// <summary>
/// Command to create a new device brand.
/// </summary>
public record CreateDeviceBrandCommand(string Name) : IRequest<Guid>;

/// <summary>
/// Handler for executing the create device brand command.
/// </summary>
public class CreateDeviceBrandCommandHandler : IRequestHandler<CreateDeviceBrandCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateDeviceBrandCommandHandler"/> class.
    /// </summary>
    public CreateDeviceBrandCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Handles creating a device brand.
    /// </summary>
    public async Task<Guid> Handle(CreateDeviceBrandCommand request, CancellationToken cancellationToken)
    {
        var exists = await _context.DeviceBrands
            .AnyAsync(b => b.Name.ToLower() == request.Name.ToLower(), cancellationToken);

        if (exists)
        {
            throw new Exception($"Brand '{request.Name}' already exists.");
        }

        var brand = new DeviceBrand
        {
            Id = Guid.NewGuid(),
            Name = request.Name
        };

        _context.DeviceBrands.Add(brand);
        await _context.SaveChangesAsync(cancellationToken);

        return brand.Id;
    }
}
