using System;

namespace UniGroup.CRM.Application.Common.Interfaces;

/// <summary>
/// Provides access to the current authenticated user's identity and client
/// information (extracted from the HTTP context / JWT claims).
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the current user's identifier, or null for anonymous/system operations.
    /// </summary>
    Guid? UserId { get; }

    /// <summary>
    /// Gets the caller's IP address, or null when unavailable.
    /// </summary>
    string? IpAddress { get; }

    /// <summary>
    /// Gets the caller's browser user agent, or null when unavailable.
    /// </summary>
    string? UserAgent { get; }
}
