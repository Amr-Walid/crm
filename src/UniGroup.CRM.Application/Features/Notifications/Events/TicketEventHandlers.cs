using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.Notifications.Commands.SendNotification;
using UniGroup.CRM.Domain.Entities;

namespace UniGroup.CRM.Application.Features.Notifications.Events;

/// <summary>
/// Notifies the newly assigned agent via InApp and Email channels.
/// </summary>
public class TicketAssignedEventHandler : INotificationHandler<TicketAssignedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketAssignedEventHandler"/> class.
    /// </summary>
    public TicketAssignedEventHandler(IApplicationDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    /// <inheritdoc />
    public async Task Handle(TicketAssignedEvent notification, CancellationToken cancellationToken)
    {
        var agent = await _context.Tickets
            .Where(t => t.Id == notification.TicketId)
            .Select(t => new { t.AssignedTo!.Email, t.AssignedTo.FirstName })
            .FirstOrDefaultAsync(cancellationToken);

        var message = $"Ticket {notification.TicketId} has been assigned to you.";

        await _sender.Send(new SendNotificationCommand(
            RecipientType: "Agent",
            RecipientId: notification.AssignedToId.ToString(),
            Channel: "InApp",
            TemplateType: "TicketAssigned",
            MessageContent: message), cancellationToken);

        await _sender.Send(new SendNotificationCommand(
            RecipientType: "Agent",
            RecipientId: notification.AssignedToId.ToString(),
            Channel: "Email",
            TemplateType: "TicketAssigned",
            MessageContent: message,
            EmailAddress: agent?.Email), cancellationToken);
    }
}

/// <summary>
/// Notifies the customer via Chatwoot when their ticket is resolved.
/// </summary>
public class TicketResolvedEventHandler : INotificationHandler<TicketResolvedEvent>
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketResolvedEventHandler"/> class.
    /// </summary>
    public TicketResolvedEventHandler(ISender sender)
    {
        _sender = sender;
    }

    /// <inheritdoc />
    public async Task Handle(TicketResolvedEvent notification, CancellationToken cancellationToken)
    {
        await _sender.Send(new SendNotificationCommand(
            RecipientType: "Customer",
            RecipientId: notification.CustomerId.ToString(),
            Channel: "WhatsApp",
            TemplateType: "TicketResolved",
            MessageContent: $"Good news! Your ticket {notification.TicketId} has been resolved. It will be closed after your confirmation.",
            ChatwootConversationId: notification.ChatwootConversationId), cancellationToken);
    }
}

/// <summary>
/// CSAT feedback loop: on ticket closure, creates a survey with a unique
/// secure token expiring after 7 days, then sends the survey link to the
/// customer via Chatwoot/WhatsApp.
/// </summary>
public class TicketClosedEventHandler : INotificationHandler<TicketClosedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketClosedEventHandler"/> class.
    /// </summary>
    public TicketClosedEventHandler(IApplicationDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    /// <inheritdoc />
    public async Task Handle(TicketClosedEvent notification, CancellationToken cancellationToken)
    {
        // One survey per ticket (unique index also enforces this at the DB level).
        var exists = await _context.CsatSurveys
            .AnyAsync(s => s.TicketId == notification.TicketId, cancellationToken);
        if (exists) return;

        var now = DateTime.UtcNow;
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

        _context.CsatSurveys.Add(new CsatSurvey
        {
            Id = Guid.NewGuid(),
            TicketId = notification.TicketId,
            CustomerId = notification.CustomerId,
            SurveyToken = token,
            SentAt = now,
            ExpiresAt = now.AddDays(7)
        });
        await _context.SaveChangesAsync(cancellationToken);

        var surveyLink = $"/surveys?token={token}";
        await _sender.Send(new SendNotificationCommand(
            RecipientType: "Customer",
            RecipientId: notification.CustomerId.ToString(),
            Channel: "WhatsApp",
            TemplateType: "CsatSurvey",
            MessageContent: $"Your ticket {notification.TicketId} is now closed. We would love your feedback: {surveyLink} (valid for 7 days).",
            ChatwootConversationId: notification.ChatwootConversationId), cancellationToken);
    }
}

/// <summary>
/// Alerts Team Leaders and Admins via InApp and Email when an SLA is breached.
/// </summary>
public class SlaBreachedEventHandler : INotificationHandler<SlaBreachedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="SlaBreachedEventHandler"/> class.
    /// </summary>
    public SlaBreachedEventHandler(IApplicationDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    /// <inheritdoc />
    public async Task Handle(SlaBreachedEvent notification, CancellationToken cancellationToken)
    {
        var message = $"SLA BREACH: Ticket {notification.TicketId} ({notification.Title}) exceeded its SLA deadline and was escalated.";

        // Notify all users in Team Leader / Admin roles.
        var leaders = await _context.Users
            .Where(u => _context.UserRoles.Any(ur => ur.UserId == u.Id &&
                        _context.Roles.Any(r => r.Id == ur.RoleId &&
                            (r.Name == "Team Leader" || r.Name == "Admin"))))
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(cancellationToken);

        foreach (var leader in leaders)
        {
            await _sender.Send(new SendNotificationCommand(
                RecipientType: "Agent",
                RecipientId: leader.Id.ToString(),
                Channel: "InApp",
                TemplateType: "SlaBreached",
                MessageContent: message), cancellationToken);

            await _sender.Send(new SendNotificationCommand(
                RecipientType: "Agent",
                RecipientId: leader.Id.ToString(),
                Channel: "Email",
                TemplateType: "SlaBreached",
                MessageContent: message,
                EmailAddress: leader.Email), cancellationToken);
        }
    }
}
