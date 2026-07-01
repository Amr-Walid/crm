using MediatR;
using Microsoft.EntityFrameworkCore;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace UniGroup.CRM.Application.Features.Devices.Commands.CreateDeviceModel;

/// <summary>
/// Command to create a new device model under a brand.
/// </summary>
public record CreateDeviceModelCommand(Guid BrandId, string Name) : IRequest<Guid>;

/// <summary>
/// Handler for executing the create device model command.
/// </summary>
public class CreateDeviceModelCommandHandler : IRequestHandler<CreateDeviceModelCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateDeviceModelCommandHandler"/> class.
    /// </summary>
    public CreateDeviceModelCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Handles creating a device model.
    /// </summary>
    public async Task<Guid> Handle(CreateDeviceModelCommand request, CancellationToken cancellationToken)
    {
        var brandExists = await _context.DeviceBrands
            .AnyAsync(b => b.Id == request.BrandId, cancellationToken);

        if (!brandExists)
        {
            throw new Exception($"Brand with ID '{request.BrandId}' does not exist.");
        }

        var exists = await _context.DeviceModels
            .AnyAsync(m => m.BrandId == request.BrandId && m.Name.ToLower() == request.Name.ToLower(), cancellationToken);

        if (exists)
        {
            throw new Exception($"Model '{request.Name}' already exists under this brand.");
        }

        var model = new DeviceModel
        {
            Id = Guid.NewGuid(),
            BrandId = request.BrandId,
            Name = request.Name
        };

        _context.DeviceModels.Add(model);
        await _context.SaveChangesAsync(cancellationToken);

        return model.Id;
    }
}
