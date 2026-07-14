using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Domain.Entities;

namespace UniGroup.CRM.Application.Features.Notifications.Commands.SendNotification;

/// <summary>
/// The notification engine: dispatches a message through the requested
/// channel (Email / WhatsApp via Chatwoot / InApp) and records the delivery
/// outcome in <see cref="NotificationLog"/>.
/// </summary>
/// <param name="RecipientType">Agent or Customer.</param>
/// <param name="RecipientId">User id, phone number, e-mail, or conversation id depending on channel.</param>
/// <param name="Channel">Email, WhatsApp, or InApp.</param>
/// <param name="TemplateType">Template identifier (e.g. TicketAssigned, SlaBreached, CsatSurvey).</param>
/// <param name="MessageContent">The compiled message text.</param>
/// <param name="EmailAddress">Explicit e-mail address for Email channel.</param>
/// <param name="ChatwootConversationId">Conversation id for WhatsApp/Chatwoot channel.</param>
public record SendNotificationCommand(
    string RecipientType,
    string RecipientId,
    string Channel,
    string TemplateType,
    string MessageContent,
    string? EmailAddress = null,
    string? ChatwootConversationId = null
) : IRequest;

/// <summary>
/// Handler that routes the notification to the proper transport and always
/// writes a <see cref="NotificationLog"/> record (Sent or Failed).
/// </summary>
public class SendNotificationCommandHandler : IRequestHandler<SendNotificationCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IChatwootClientService _chatwootClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendNotificationCommandHandler"/> class.
    /// </summary>
    public SendNotificationCommandHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        IChatwootClientService chatwootClient)
    {
        _context = context;
        _emailService = emailService;
        _chatwootClient = chatwootClient;
    }

    /// <inheritdoc />
    public async Task Handle(SendNotificationCommand request, CancellationToken cancellationToken)
    {
        var sent = false;
        string? error = null;

        switch (request.Channel)
        {
            case "Email":
                if (string.IsNullOrEmpty(request.EmailAddress))
                {
                    error = "No e-mail address supplied for Email channel.";
                }
                else
                {
                    sent = await _emailService.SendAsync(
                        request.EmailAddress,
                        $"UniGroup CRM — {request.TemplateType}",
                        request.MessageContent,
                        cancellationToken);
                    if (!sent) error = "SMTP dispatch failed or not configured.";
                }
                break;

            case "WhatsApp":
                if (string.IsNullOrEmpty(request.ChatwootConversationId))
                {
                    error = "No Chatwoot conversation id supplied for WhatsApp channel.";
                }
                else
                {
                    sent = await _chatwootClient.SendMessageAsync(
                        request.ChatwootConversationId,
                        request.MessageContent,
                        isPrivate: false,
                        ct: cancellationToken);
                    if (!sent) error = "Chatwoot dispatch failed or not configured.";
                }
                break;

            case "InApp":
                // The NotificationLog row itself IS the in-app notification store.
                sent = true;
                break;

            default:
                error = $"Unknown channel '{request.Channel}'.";
                break;
        }

        _context.NotificationLogs.Add(new NotificationLog
        {
            Id = Guid.NewGuid(),
            RecipientType = request.RecipientType,
            RecipientId = request.RecipientId,
            Channel = request.Channel,
            TemplateType = request.TemplateType,
            Status = sent ? "Sent" : "Failed",
            MessageContent = request.MessageContent,
            ErrorMessage = error,
            SentAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
    }
}
