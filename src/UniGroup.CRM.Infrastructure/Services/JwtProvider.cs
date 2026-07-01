using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Domain.Entities;

namespace UniGroup.CRM.Infrastructure.Services;

/// <summary>
/// Service to generate JWT access tokens and secure refresh tokens.
/// </summary>
public class JwtProvider : IJwtProvider
{
    private readonly JwtOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtProvider"/> class.
    /// </summary>
    /// <param name="options">The configured JWT options.</param>
    public JwtProvider(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Generates a JWT access token for a user.
    /// </summary>
    /// <param name="user">The user entity.</param>
    /// <param name="roles">Roles assigned to the user.</param>
    /// <returns>A TokenResult containing the signed JWT token string and its expiration.</returns>
    public TokenResult GenerateToken(ApplicationUser user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("username", user.UserName ?? string.Empty),
            new Claim("name", $"{user.FirstName} {user.LastName}")
        };

        // Add role claims
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiration = DateTime.UtcNow.AddMinutes(_options.TokenLifetimeInMinutes);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiration,
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return new TokenResult(tokenString, expiration);
    }

    /// <summary>
    /// Generates a cryptographically strong random token for refresh token mechanism.
    /// </summary>
    /// <param name="ipAddress">The IP address of the requester.</param>
    /// <returns>A new <see cref="RefreshToken"/> object.</returns>
    public RefreshToken GenerateRefreshToken(string ipAddress)
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = Convert.ToBase64String(randomNumber),
            ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenLifetimeInDays),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress
        };
    }
}
