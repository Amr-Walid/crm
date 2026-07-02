using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.Departments.Queries.Common;

namespace UniGroup.CRM.Application.Features.Departments.Queries.GetDepartments;

/// <summary>
/// Query to retrieve all departments.
/// </summary>
public record GetDepartmentsQuery : IRequest<List<DepartmentDto>>;

/// <summary>
/// Handler for executing the get departments query.
/// </summary>
public class GetDepartmentsQueryHandler : IRequestHandler<GetDepartmentsQuery, List<DepartmentDto>>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetDepartmentsQueryHandler"/> class.
    /// </summary>
    public GetDepartmentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<List<DepartmentDto>> Handle(GetDepartmentsQuery request, CancellationToken cancellationToken)
    {
        var departments = await _context.Departments
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);

        return departments.Select(d => new DepartmentDto(
            d.Id,
            d.Name,
            d.Description,
            d.IsActive,
            d.CreatedAt
        )).ToList();
    }
}
