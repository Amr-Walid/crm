using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Domain.Entities;

namespace UniGroup.CRM.Application.Features.Departments.Commands.CreateDepartment;

/// <summary>
/// Command to create a new department.
/// </summary>
public record CreateDepartmentCommand(
    string Name,
    string? Description,
    bool IsActive
) : IRequest<Guid>;

/// <summary>
/// Handler for executing the create department command.
/// </summary>
public class CreateDepartmentCommandHandler : IRequestHandler<CreateDepartmentCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateDepartmentCommandHandler"/> class.
    /// </summary>
    public CreateDepartmentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<Guid> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new Exception("Department name is required.");
        }

        // Check if name is unique
        var exists = await _context.Departments
            .AnyAsync(d => d.Name == request.Name, cancellationToken);
        if (exists)
        {
            throw new Exception($"A department named '{request.Name}' already exists.");
        }

        var department = new Department
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Departments.Add(department);
        await _context.SaveChangesAsync(cancellationToken);

        return department.Id;
    }
}
