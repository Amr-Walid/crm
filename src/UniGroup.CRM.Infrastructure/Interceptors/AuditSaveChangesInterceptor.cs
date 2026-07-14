using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Domain.Entities;
using UniGroup.CRM.Infrastructure.Channels;

namespace UniGroup.CRM.Infrastructure.Interceptors;

/// <summary>
/// EF Core 9 interceptor that captures every insert, update and delete and
/// pushes an <see cref="AuditLog"/> snapshot to a bounded channel for
/// asynchronous batch persistence — keeping main transactions extremely short.
/// </summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly AuditLogChannel _auditChannel;
    private readonly ICurrentUserService _currentUserService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditSaveChangesInterceptor"/> class.
    /// </summary>
    public AuditSaveChangesInterceptor(
        AuditLogChannel auditChannel,
        ICurrentUserService currentUserService)
    {
        _auditChannel = auditChannel;
        _currentUserService = currentUserService;
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await CaptureAuditEntriesAsync(eventData.Context, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            CaptureAuditEntriesAsync(eventData.Context, CancellationToken.None)
                .AsTask().GetAwaiter().GetResult();
        }

        return base.SavingChanges(eventData, result);
    }

    private async ValueTask CaptureAuditEntriesAsync(DbContext context, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var clientInfo = new ClientInfo
        {
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent
        };
        var now = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // Guardrail 8.1: never audit the audit pipeline itself (infinite loop bypass).
            if (entry.Entity is AuditLog || entry.Entity is ProcessedWebhookEvent || entry.Entity is NotificationLog)
            {
                continue;
            }

            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Action = entry.State.ToString(),
                TableName = entry.Metadata.GetTableName() ?? entry.Metadata.DisplayName(),
                RecordId = GetPrimaryKeyValue(entry),
                ClientInfo = clientInfo,
                CreatedAt = now
            };

            switch (entry.State)
            {
                case EntityState.Added:
                    auditLog.AfterValue = SerializeProperties(entry, useOriginal: false);
                    break;

                case EntityState.Deleted:
                    auditLog.BeforeValue = SerializeProperties(entry, useOriginal: true);
                    break;

                case EntityState.Modified:
                    var before = new Dictionary<string, object?>();
                    var after = new Dictionary<string, object?>();
                    foreach (var property in entry.Properties)
                    {
                        if (property.IsModified && !Equals(property.OriginalValue, property.CurrentValue))
                        {
                            before[property.Metadata.Name] = property.OriginalValue;
                            after[property.Metadata.Name] = property.CurrentValue;
                        }
                    }

                    // Skip no-op updates with no actual value changes.
                    if (after.Count == 0) continue;

                    auditLog.BeforeValue = JsonSerializer.Serialize(before, JsonOptions);
                    auditLog.AfterValue = JsonSerializer.Serialize(after, JsonOptions);
                    break;
            }

            await _auditChannel.Writer.WriteAsync(auditLog, cancellationToken);
        }
    }

    private static string GetPrimaryKeyValue(EntityEntry entry)
    {
        var keyProperty = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
        return keyProperty?.CurrentValue?.ToString() ?? "Unknown";
    }

    private static string SerializeProperties(EntityEntry entry, bool useOriginal)
    {
        var values = entry.Properties.ToDictionary(
            p => p.Metadata.Name,
            p => useOriginal ? p.OriginalValue : p.CurrentValue);
        return JsonSerializer.Serialize(values, JsonOptions);
    }
}
