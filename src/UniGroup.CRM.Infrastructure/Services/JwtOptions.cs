namespace UniGroup.CRM.Infrastructure.Services;

/// <summary>
/// Configurations for JSON Web Token (JWT) settings.
/// </summary>
public class JwtOptions
{
    /// <summary>
    /// Gets or sets the security key used to sign tokens.
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the issuer of the token.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the audience of the token.
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the lifetime of the access token in minutes.
    /// </summary>
    public int TokenLifetimeInMinutes { get; set; }

    /// <summary>
    /// Gets or sets the lifetime of the refresh token in days.
    /// </summary>
    public int RefreshTokenLifetimeInDays { get; set; }
}
