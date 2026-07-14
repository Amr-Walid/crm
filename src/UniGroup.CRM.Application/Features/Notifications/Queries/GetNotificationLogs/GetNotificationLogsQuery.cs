using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.Tickets.Queries.GetTicketsList;

namespace UniGroup.CRM.Application.Features.Notifications.Queries.GetNotificationLogs;

/// <summary>
/// A single notification dispatch record.
/// </summary>
/// <param name="Id">Log entry id.</param>
/// <param name="RecipientType">Agent / Customer / Role.</param>
/// <param name="RecipientId">Recipient identifier.</param>
/// <param name="Channel">InApp / Email / WhatsApp.</param>
/// <param name="TemplateType">Logical template name.</param>
/// <param name="Status">Sent / Failed.</param>
/// <param name="MessageContent">The dispatched message body.</param>
/// <param name="ErrorMessage">Failure details when Status is Failed.</param>
/// <param name="SentAt">UTC dispatch timestamp.</param>
public record NotificationLogDto(
    Guid Id,
    string RecipientType,
    string RecipientId,
    string Channel,
    string TemplateType,
    string Status,
    string? MessageContent,
    string? ErrorMessage,
    DateTime SentAt);

/// <summary>
/// Paged, filterable query over the notification dispatch log. Admin only.
/// </summary>
/// <param name="Channel">Optional filter by channel (InApp/Email/WhatsApp).</param>
/// <param name="Status">Optional filter by status (Sent/Failed).</param>
/// <param name="TemplateType">Optional filter by template type.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Page size (max 100).</param>
public record GetNotificationLogsQuery(
    string? Channel,
    string? Status,
    string? TemplateType,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<NotificationLogDto>>;

/// <summary>
/// Handler executing the filtered, paged notification log query (newest first).
/// </summary>
public class GetNotificationLogsQueryHandler
    : IRequestHandler<GetNotificationLogsQuery, PagedResult<NotificationLogDto>>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetNotificationLogsQueryHandler"/> class.
    /// </summary>
    public GetNotificationLogsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<PagedResult<NotificationLogDto>> Handle(
        GetNotificationLogsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _context.NotificationLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Channel))
        {
            query = query.Where(n => n.Channel == request.Channel);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(n => n.Status == request.Status);
        }

        if (!string.IsNullOrWhiteSpace(request.TemplateType))
        {
            query = query.Where(n => n.TemplateType == request.TemplateType);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationLogDto(
                n.Id, n.RecipientType, n.RecipientId, n.Channel, n.TemplateType,
                n.Status, n.MessageContent, n.ErrorMessage, n.SentAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationLogDto>(items, totalCount, page, pageSize);
    }
}
