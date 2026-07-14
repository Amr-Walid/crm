using System.Threading.Channels;
using UniGroup.CRM.Domain.Entities;

namespace UniGroup.CRM.Infrastructure.Channels;

/// <summary>
/// Bounded in-memory channel decoupling the Chatwoot webhook controller from
/// the background processor (Inbox pattern). Capacity 10,000 with
/// <see cref="BoundedChannelFullMode.Wait"/> to prevent OutOfMemory crashes
/// under extreme traffic while guaranteeing no payload loss.
/// </summary>
public class ChatwootWebhookChannel
{
    private readonly Channel<string> _channel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatwootWebhookChannel"/> class.
    /// </summary>
    public ChatwootWebhookChannel()
    {
        var options = new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<string>(options);
    }

    /// <summary>
    /// Gets the writer used by the webhook controller to enqueue raw payloads.
    /// </summary>
    public ChannelWriter<string> Writer => _channel.Writer;

    /// <summary>
    /// Gets the reader consumed by the <c>ChatwootWebhookProcessor</c> background service.
    /// </summary>
    public ChannelReader<string> Reader => _channel.Reader;
}

/// <summary>
/// Bounded in-memory channel decoupling the EF Core audit interceptor from the
/// batch database writer (Outbox pattern). Capacity 5,000 with
/// <see cref="BoundedChannelFullMode.Wait"/> so main transactions stay short
/// without ever dropping audit records.
/// </summary>
public class AuditLogChannel
{
    private readonly Channel<AuditLog> _channel;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogChannel"/> class.
    /// </summary>
    public AuditLogChannel()
    {
        var options = new BoundedChannelOptions(5_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<AuditLog>(options);
    }

    /// <summary>
    /// Gets the writer used by <c>AuditSaveChangesInterceptor</c> to enqueue audit entries.
    /// </summary>
    public ChannelWriter<AuditLog> Writer => _channel.Writer;

    /// <summary>
    /// Gets the reader consumed by the <c>AuditLogProcessor</c> background service.
    /// </summary>
    public ChannelReader<AuditLog> Reader => _channel.Reader;
}
