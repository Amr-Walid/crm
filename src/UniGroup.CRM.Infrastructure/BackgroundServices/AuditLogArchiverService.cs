using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UniGroup.CRM.Infrastructure.Data;

namespace UniGroup.CRM.Infrastructure.BackgroundServices;

/// <summary>
/// Daily background job that purges audit log entries older than the
/// configured retention window (<c>Audit:AuditLogRetentionMonths</c>, default
/// 6 months) to prevent data bloat and keep audit queries fast.
/// </summary>
public class AuditLogArchiverService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuditLogArchiverService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogArchiverService"/> class.
    /// </summary>
    public AuditLogArchiverService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AuditLogArchiverService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audit Log Archiver Service started.");

        // Run once at startup, then daily.
        await ArchiveOldLogsAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ArchiveOldLogsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }

        _logger.LogInformation("Audit Log Archiver Service stopped.");
    }

    private async Task ArchiveOldLogsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var retentionMonths = _configuration.GetValue<int?>("Audit:AuditLogRetentionMonths") ?? 6;
            var cutoff = DateTime.UtcNow.AddMonths(-retentionMonths);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // EF Core 9 bulk delete — no entity materialization.
            var deleted = await db.AuditLogs
                .Where(a => a.CreatedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted > 0)
            {
                _logger.LogInformation(
                    "Audit archiver purged {Count} entries older than {Cutoff:yyyy-MM-dd}.", deleted, cutoff);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during audit log archiving run.");
        }
    }
}
