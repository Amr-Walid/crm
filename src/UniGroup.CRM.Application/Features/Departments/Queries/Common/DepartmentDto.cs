using System;

namespace UniGroup.CRM.Application.Features.Departments.Queries.Common;

/// <summary>
/// DTO representing a company department.
/// </summary>
public record DepartmentDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt
);
