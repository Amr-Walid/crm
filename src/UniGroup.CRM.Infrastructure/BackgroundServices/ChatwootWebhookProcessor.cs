using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Domain.Entities;
using UniGroup.CRM.Domain.Enums;
using UniGroup.CRM.Infrastructure.Channels;
using UniGroup.CRM.Infrastructure.Data;

namespace UniGroup.CRM.Infrastructure.BackgroundServices;

/// <summary>
/// Background consumer for the Chatwoot webhook bounded channel. Applies
/// idempotency via <see cref="ProcessedWebhookEvent"/>, resolves customers by
/// phone, links messages to active tickets by ChatwootConversationId, and
/// auto-creates General Inquiry tickets for new conversations. Uses Polly
/// retries (3x exponential backoff) and supports graceful shutdown draining.
/// </summary>
public class ChatwootWebhookProcessor : BackgroundService
{
    private readonly ChatwootWebhookChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChatwootWebhookProcessor> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatwootWebhookProcessor"/> class.
    /// </summary>
    public ChatwootWebhookProcessor(
        ChatwootWebhookChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<ChatwootWebhookProcessor> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _retryPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                (ex, delay) => _logger.LogWarning(
                    "Transient fault in webhook processing. Retrying after {Delay}s: {Message}",
                    delay.TotalSeconds, ex.Message));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Chatwoot Webhook Processor started.");
        try
        {
            while (await _channel.Reader.WaitToReadAsync(stoppingToken))
            {
                while (_channel.Reader.TryRead(out var payload))
                {
                    await ProcessSafelyAsync(payload, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown: drain any payloads still buffered in the channel.
            _logger.LogInformation("Shutdown requested. Draining remaining webhook payloads...");
            while (_channel.Reader.TryRead(out var payload))
            {
                await ProcessSafelyAsync(payload, CancellationToken.None);
            }
        }

        _logger.LogInformation("Chatwoot Webhook Processor stopped.");
    }

    private async Task ProcessSafelyAsync(string payload, CancellationToken cancellationToken)
    {
        try
        {
            await _retryPolicy.ExecuteAsync(ct => ProcessPayloadAsync(payload, ct), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process webhook payload after retries.");
        }
    }

    private async Task ProcessPayloadAsync(string payload, CancellationToken cancellationToken)
    {
        using var jsonDoc = JsonDocument.Parse(payload);
        var root = jsonDoc.RootElement;

        // Only handle incoming customer messages.
        if (!root.TryGetProperty("event", out var eventProp)) return;
        var eventType = eventProp.GetString() ?? string.Empty;
        if (eventType != "message_created") return;

        if (root.TryGetProperty("message_type", out var msgTypeProp))
        {
            var messageType = msgTypeProp.ValueKind == JsonValueKind.String
                ? msgTypeProp.GetString()
                : msgTypeProp.GetRawText();
            if (messageType != "incoming" && messageType != "0") return;
        }

        // Extract the unique event id for idempotency.
        var eventId = ExtractString(root, "id");
        if (string.IsNullOrEmpty(eventId)) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // ===== Idempotency check (guardrail: at-least-once delivery safety) =====
        var alreadyProcessed = await db.ProcessedWebhookEvents
            .AsNoTracking()
            .AnyAsync(e => e.EventId == eventId, cancellationToken);
        if (alreadyProcessed)
        {
            _logger.LogInformation("Duplicate webhook event {EventId} discarded.", eventId);
            return;
        }

        // ===== Extract conversation + sender =====
        var conversationId = ExtractConversationId(root);
        var (senderName, senderPhone, senderEmail) = ExtractSender(root);
        var content = ExtractString(root, "content") ?? string.Empty;

        // ===== Resolve or create the customer by phone =====
        Customer? customer = null;
        if (!string.IsNullOrEmpty(senderPhone))
        {
            var normalizedPhone = senderPhone.Replace("+2", "").Trim();
            customer = await db.CustomerPhones
                .Where(p => p.Phone == normalizedPhone || p.Phone == senderPhone)
                .Select(p => p.Customer)
                .FirstOrDefaultAsync(cancellationToken);

            if (customer == null)
            {
                customer = new Customer
                {
                    Id = Guid.NewGuid(),
                    Name = string.IsNullOrEmpty(senderName) ? $"Chatwoot Contact {senderPhone}" : senderName,
                    Email = senderEmail,
                    CreatedAt = DateTime.UtcNow
                };
                db.Customers.Add(customer);
                db.CustomerPhones.Add(new CustomerPhone
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer.Id,
                    Phone = normalizedPhone,
                    IsPrimary = true
                });
            }
        }

        if (customer != null && !string.IsNullOrEmpty(conversationId))
        {
            // ===== Conversation-level idempotency: reuse the active ticket =====
            var activeTicket = await db.Tickets
                .Where(t => t.ChatwootConversationId == conversationId &&
                            t.Status != TicketStatus.Resolved &&
                            t.Status != TicketStatus.Closed &&
                            t.Status != TicketStatus.Cancelled)
                .FirstOrDefaultAsync(cancellationToken);

            // System actor: first (oldest) user acts as the automation identity.
            var systemUserId = await db.Users
                .OrderBy(u => u.CreatedAt)
                .Select(u => u.Id)
                .FirstAsync(cancellationToken);

            if (activeTicket != null)
            {
                // Append message as internal note to the existing ticket.
                db.InternalNotes.Add(new InternalNote
                {
                    Id = Guid.NewGuid(),
                    TicketId = activeTicket.Id,
                    AuthorId = systemUserId,
                    Content = $"[Chatwoot message] {content}",
                    CreatedAt = DateTime.UtcNow
                });
                activeTicket.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Auto-create a General Inquiry ticket for the new conversation.
                var numberGenerator = scope.ServiceProvider.GetRequiredService<ITicketNumberGenerator>();
                var ticketId = await numberGenerator.GenerateAsync(cancellationToken);
                var now = DateTime.UtcNow;

                var ticket = new Ticket
                {
                    Id = ticketId,
                    CustomerId = customer.Id,
                    Title = $"Chatwoot Conversation - {conversationId}",
                    Description = content,
                    Category = TicketCategory.GeneralInquiry,
                    Status = TicketStatus.New,
                    Priority = TicketPriority.Medium,
                    ChatwootConversationId = conversationId,
                    SlaDeadline = now.AddHours(72), // Medium priority SLA
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.Tickets.Add(ticket);
                db.TicketHistories.Add(new TicketHistory
                {
                    Id = Guid.NewGuid(),
                    TicketId = ticketId,
                    FromStatus = null,
                    ToStatus = TicketStatus.New,
                    ChangedById = systemUserId,
                    Note = "Ticket auto-created from Chatwoot conversation.",
                    CreatedAt = now
                });
            }
        }

        // ===== Mark event as processed — same SaveChanges (single transaction) =====
        db.ProcessedWebhookEvents.Add(new ProcessedWebhookEvent
        {
            EventId = eventId,
            ProcessedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Webhook event {EventId} processed successfully.", eventId);
    }

    private static string? ExtractString(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var prop)) return null;
        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.GetRawText(),
            _ => null
        };
    }

    private static string? ExtractConversationId(JsonElement root)
    {
        if (root.TryGetProperty("conversation", out var conv) &&
            conv.ValueKind == JsonValueKind.Object &&
            conv.TryGetProperty("id", out var convId))
        {
            return convId.ValueKind == JsonValueKind.Number ? convId.GetRawText() : convId.GetString();
        }
        return null;
    }

    private static (string? Name, string? Phone, string? Email) ExtractSender(JsonElement root)
    {
        if (!root.TryGetProperty("sender", out var sender) || sender.ValueKind != JsonValueKind.Object)
        {
            return (null, null, null);
        }

        string? name = sender.TryGetProperty("name", out var n) ? n.GetString() : null;
        string? phone = sender.TryGetProperty("phone_number", out var p) ? p.GetString() : null;
        string? email = sender.TryGetProperty("email", out var e) && e.ValueKind == JsonValueKind.String
            ? e.GetString()
            : null;
        return (name, phone, email);
    }
}
