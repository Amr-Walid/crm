namespace UniGroup.CRM.Domain.Entities;

/// <summary>
/// Represents a refresh token used to generate new JWT tokens for users.
/// </summary>
public class RefreshToken
{
    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the actual token string.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the token expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Gets a value indicating whether the token has expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>
    /// Gets or sets when the token was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the IP address that requested this token.
    /// </summary>
    public string CreatedByIp { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when this token was revoked.
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Gets or sets the IP address that revoked this token.
    /// </summary>
    public string? RevokedByIp { get; set; }

    /// <summary>
    /// Gets or sets the token that replaced this token if it has been rotated.
    /// </summary>
    public string? ReplacedByToken { get; set; }

    /// <summary>
    /// Gets a value indicating whether the token is active (neither revoked nor expired).
    /// </summary>
    public bool IsActive => RevokedAt == null && !IsExpired;

    /// <summary>
    /// Gets or sets the foreign key of the user associated with this token.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the user associated with this token.
    /// </summary>
    public virtual ApplicationUser User { get; set; } = null!;
}
