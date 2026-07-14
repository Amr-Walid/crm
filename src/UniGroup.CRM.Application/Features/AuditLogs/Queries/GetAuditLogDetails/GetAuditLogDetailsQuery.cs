using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;

namespace UniGroup.CRM.Application.Features.AuditLogs.Queries.GetAuditLogDetails;

/// <summary>
/// Full audit entry including before/after JSON payloads.
/// </summary>
/// <param name="Id">Audit entry id.</param>
/// <param name="UserId">Acting user id (null for system actions).</param>
/// <param name="Action">INSERT / UPDATE / DELETE.</param>
/// <param name="TableName">The affected table.</param>
/// <param name="RecordId">The affected record's primary key.</param>
/// <param name="BeforeValue">JSON snapshot before the change (UPDATE/DELETE).</param>
/// <param name="AfterValue">JSON snapshot after the change (INSERT/UPDATE).</param>
/// <param name="IpAddress">Captured client IP address.</param>
/// <param name="UserAgent">Captured client user agent.</param>
/// <param name="CreatedAt">UTC timestamp of the change.</param>
public record AuditLogDetailsDto(
    Guid Id,
    Guid? UserId,
    string Action,
    string TableName,
    string RecordId,
    string? BeforeValue,
    string? AfterValue,
    string? IpAddress,
    string? UserAgent,
    DateTime CreatedAt);

/// <summary>
/// Query returning a single audit entry with full payloads, or null when not found.
/// </summary>
/// <param name="Id">The audit entry id.</param>
public record GetAuditLogDetailsQuery(Guid Id) : IRequest<AuditLogDetailsDto?>;

/// <summary>
/// Handler fetching the audit entry by id.
/// </summary>
public class GetAuditLogDetailsQueryHandler
    : IRequestHandler<GetAuditLogDetailsQuery, AuditLogDetailsDto?>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetAuditLogDetailsQueryHandler"/> class.
    /// </summary>
    public GetAuditLogDetailsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<AuditLogDetailsDto?> Handle(
        GetAuditLogDetailsQuery request, CancellationToken cancellationToken)
    {
        return await _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.Id == request.Id)
            .Select(a => new AuditLogDetailsDto(
                a.Id, a.UserId, a.Action, a.TableName, a.RecordId,
                a.BeforeValue, a.AfterValue,
                a.ClientInfo.IpAddress, a.ClientInfo.UserAgent,
                a.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
