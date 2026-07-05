using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;

namespace UniGroup.CRM.Domain.Entities;

/// <summary>
/// Represents the application user, extending the ASP.NET Core Identity user with custom properties.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// Gets or sets the first name of the user.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last name of the user.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the user was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets a value indicating whether the user account is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the identifier of the department this user belongs to.
    /// Null for system accounts or users not yet assigned to a department.
    /// </summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>
    /// Gets or sets the department this user belongs to.
    /// </summary>
    public virtual Department? Department { get; set; }

    /// <summary>
    /// Gets or sets the refresh tokens associated with the user.
    /// </summary>
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    /// <summary>
    /// Gets or sets the call records handled by this agent.
    /// </summary>
    public virtual ICollection<Call> Calls { get; set; } = new List<Call>();

    /// <summary>
    /// Gets or sets the tickets assigned to this agent.
    /// </summary>
    public virtual ICollection<Ticket> AssignedTickets { get; set; } = new List<Ticket>();
}
