using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using UniGroup.CRM.Domain.Entities;
using UniGroup.CRM.Infrastructure.Channels;
using UniGroup.CRM.Infrastructure.Data;

namespace UniGroup.CRM.Infrastructure.BackgroundServices;

/// <summary>
/// Background consumer for the audit log bounded channel. Buffers entries and
/// bulk-inserts them in batches with Polly retries. On shutdown, drains the
/// channel and flushes remaining records before exiting (graceful shutdown).
/// </summary>
public class AuditLogProcessor : BackgroundService
{
    private const int BatchSize = 100;

    private readonly AuditLogChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogProcessor> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogProcessor"/> class.
    /// </summary>
    public AuditLogProcessor(
        AuditLogChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<AuditLogProcessor> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _retryPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                (ex, delay) => _logger.LogWarning(
                    "Transient fault flushing audit logs. Retrying after {Delay}s: {Message}",
                    delay.TotalSeconds, ex.Message));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audit Log Processor started.");
        var buffer = new List<AuditLog>(BatchSize);

        try
        {
            while (await _channel.Reader.WaitToReadAsync(stoppingToken))
            {
                while (_channel.Reader.TryRead(out var log))
                {
                    buffer.Add(log);
                    if (buffer.Count >= BatchSize)
                    {
                        await FlushAsync(buffer, CancellationToken.None);
                    }
                }

                // Drained the channel — flush whatever is buffered.
                if (buffer.Count > 0)
                {
                    await FlushAsync(buffer, CancellationToken.None);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown: drain remaining entries and flush before exit.
            _logger.LogInformation("Shutdown requested. Flushing remaining audit logs...");
            while (_channel.Reader.TryRead(out var log))
            {
                buffer.Add(log);
            }

            if (buffer.Count > 0)
            {
                await FlushAsync(buffer, CancellationToken.None);
            }
        }

        _logger.LogInformation("Audit Log Processor stopped.");
    }

    private async Task FlushAsync(List<AuditLog> buffer, CancellationToken cancellationToken)
    {
        try
        {
            await _retryPolicy.ExecuteAsync(async ct =>
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.AuditLogs.AddRange(buffer);
                await db.SaveChangesAsync(ct);
            }, cancellationToken);

            _logger.LogDebug("Flushed {Count} audit log entries.", buffer.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush {Count} audit log entries after retries.", buffer.Count);
        }
        finally
        {
            buffer.Clear();
        }
    }
}
