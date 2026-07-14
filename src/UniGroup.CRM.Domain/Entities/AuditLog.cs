using System;

namespace UniGroup.CRM.Domain.Entities;

/// <summary>
/// Client (browser/network) information captured alongside an audit entry.
/// Mapped as an EF Core 9 Complex Type producing columns
/// ClientInfo_IpAddress and ClientInfo_UserAgent.
/// </summary>
public class ClientInfo
{
    /// <summary>
    /// Gets or sets the IP address of the actor performing the operation.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Gets or sets the browser user agent of the actor performing the operation.
    /// </summary>
    public string? UserAgent { get; set; }
}

/// <summary>
/// Represents an automatic audit trail record capturing every insert, update
/// and delete performed through the application (the operations "black box").
/// </summary>
public class AuditLog
{
    /// <summary>
    /// Gets or sets the unique identifier of the audit entry.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who performed the action.
    /// Null for system-initiated (background) operations. FK → AspNetUsers (SetNull).
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Gets or sets the operation type (Added, Modified, Deleted).
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the affected database table.
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the primary key value of the affected record.
    /// </summary>
    public string RecordId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON snapshot of original values (before update/delete).
    /// </summary>
    public string? BeforeValue { get; set; }

    /// <summary>
    /// Gets or sets the JSON snapshot of new values (after insert/update).
    /// </summary>
    public string? AfterValue { get; set; }

    /// <summary>
    /// Gets or sets the client information (IP address and user agent) — EF Core 9 Complex Type.
    /// </summary>
    public ClientInfo ClientInfo { get; set; } = new ClientInfo();

    /// <summary>
    /// Gets or sets the UTC timestamp of the operation (indexed).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
