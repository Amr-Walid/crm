using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Features.Departments.Commands.CreateDepartment;
using UniGroup.CRM.Application.Features.Departments.Queries.Common;
using UniGroup.CRM.Application.Features.Departments.Queries.GetDepartments;

namespace UniGroup.CRM.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DepartmentsController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="DepartmentsController"/> class.
    /// </summary>
    public DepartmentsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Gets all departments.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<DepartmentDto>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var query = new GetDepartmentsQuery();
            var result = await _sender.Send(query, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Creates a new department.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Guid))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var command = new CreateDepartmentCommand(request.Name, request.Description, request.IsActive);
            var departmentId = await _sender.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetAll), new { id = departmentId }, departmentId);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public record CreateDepartmentRequest(
    string Name,
    string? Description,
    bool IsActive
);
