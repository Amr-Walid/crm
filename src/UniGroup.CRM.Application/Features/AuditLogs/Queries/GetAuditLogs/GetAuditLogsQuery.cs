using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.Tickets.Queries.GetTicketsList;

namespace UniGroup.CRM.Application.Features.AuditLogs.Queries.GetAuditLogs;

/// <summary>
/// Summary row for the audit logs list (before/after payloads excluded).
/// </summary>
/// <param name="Id">Audit entry id.</param>
/// <param name="UserId">Acting user id (null for system actions).</param>
/// <param name="Action">INSERT / UPDATE / DELETE.</param>
/// <param name="TableName">The affected table.</param>
/// <param name="RecordId">The affected record's primary key.</param>
/// <param name="IpAddress">Captured client IP address.</param>
/// <param name="CreatedAt">UTC timestamp of the change.</param>
public record AuditLogListItemDto(
    Guid Id,
    Guid? UserId,
    string Action,
    string TableName,
    string RecordId,
    string? IpAddress,
    DateTime CreatedAt);

/// <summary>
/// Paged, filterable query over the audit trail. Admin only.
/// </summary>
/// <param name="TableName">Optional filter by affected table.</param>
/// <param name="Action">Optional filter by action (INSERT/UPDATE/DELETE).</param>
/// <param name="UserId">Optional filter by acting user.</param>
/// <param name="From">Optional inclusive lower bound on CreatedAt.</param>
/// <param name="To">Optional inclusive upper bound on CreatedAt.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Page size (max 100).</param>
public record GetAuditLogsQuery(
    string? TableName,
    string? Action,
    Guid? UserId,
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<AuditLogListItemDto>>;

/// <summary>
/// Handler executing the filtered, paged audit log query (newest first).
/// </summary>
public class GetAuditLogsQueryHandler
    : IRequestHandler<GetAuditLogsQuery, PagedResult<AuditLogListItemDto>>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetAuditLogsQueryHandler"/> class.
    /// </summary>
    public GetAuditLogsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<PagedResult<AuditLogListItemDto>> Handle(
        GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _context.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.TableName))
        {
            query = query.Where(a => a.TableName == request.TableName);
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            query = query.Where(a => a.Action == request.Action);
        }

        if (request.UserId.HasValue)
        {
            query = query.Where(a => a.UserId == request.UserId.Value);
        }

        if (request.From.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= request.To.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogListItemDto(
                a.Id, a.UserId, a.Action, a.TableName, a.RecordId,
                a.ClientInfo.IpAddress, a.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogListItemDto>(items, totalCount, page, pageSize);
    }
}
