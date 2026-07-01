using Microsoft.AspNetCore.Identity;

namespace UniGroup.CRM.Domain.Entities;

/// <summary>
/// Represents the custom role in the application, extending the ASP.NET Core Identity role.
/// </summary>
public class ApplicationRole : IdentityRole<Guid>
{
    /// <summary>
    /// Gets or sets a description for the role.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the role was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Initializes a new instance of the ApplicationRole class.
    /// </summary>
    public ApplicationRole() : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the ApplicationRole class with a name.
    /// </summary>
    public ApplicationRole(string roleName) : base(roleName)
    {
    }
}
