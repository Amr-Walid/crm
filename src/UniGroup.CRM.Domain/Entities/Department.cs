using System;
using System.Collections.Generic;

namespace UniGroup.CRM.Domain.Entities;

/// <summary>
/// Represents a company department in the CRM system.
/// </summary>
public class Department
{
    /// <summary>
    /// Gets or sets the unique identifier of the department.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the department (e.g. Customer Service, Technical Support).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the department.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the department is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets when the department was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the tickets assigned to this department.
    /// </summary>
    public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();

    /// <summary>
    /// Gets or sets the users (agents) who belong to this department.
    /// </summary>
    public virtual ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
}
