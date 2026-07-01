using UniGroup.CRM.Domain.Entities;

namespace UniGroup.CRM.Application.Common.Interfaces;

/// <summary>
/// Result of generating a JWT token.
/// </summary>
public record TokenResult(string Token, DateTime Expiration);

/// <summary>
/// Defines methods for generating JSON Web Tokens (JWT) and refresh tokens.
/// </summary>
public interface IJwtProvider
{
    /// <summary>
    /// Generates a JWT access token for a given user and their roles.
    /// </summary>
    /// <param name="user">The application user.</param>
    /// <param name="roles">The roles assigned to the user.</param>
    /// <returns>A TokenResult containing the token string and its expiration date.</returns>
    TokenResult GenerateToken(ApplicationUser user, IList<string> roles);

    /// <summary>
    /// Generates a cryptographically secure refresh token.
    /// </summary>
    /// <param name="ipAddress">The IP address requesting the token.</param>
    /// <returns>A new <see cref="RefreshToken"/> object.</returns>
    RefreshToken GenerateRefreshToken(string ipAddress);
}
