namespace UniGroup.CRM.Client.Models;

/// <summary>
/// The persisted authentication session stored in browser localStorage.
/// Mirrors <c>AuthResponse</c> from the Application layer plus decoded claims.
/// </summary>
public class StoredSession
{
    /// <summary>The raw JWT access token.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>UTC expiry of the access token.</summary>
    public DateTime TokenExpiration { get; set; }

    /// <summary>The opaque refresh token issued at login.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>User e-mail address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>User first name.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>User last name.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>The user id (JWT <c>sub</c> claim).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Roles decoded from the JWT role claims.</summary>
    public List<string> Roles { get; set; } = [];

    /// <summary>Full display name.</summary>
    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>Whether the access token is expired (30s clock skew).</summary>
    public bool IsExpired => TokenExpiration <= DateTime.UtcNow.AddSeconds(30);
}
