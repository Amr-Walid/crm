namespace UniGroup.CRM.Application.Features.Auth.Common;

/// <summary>
/// Result returned after a successful registration or login operation.
/// </summary>
public record AuthResponse(
    string Token,
    DateTime TokenExpiration,
    string RefreshToken,
    string Email,
    string FirstName,
    string LastName
);
