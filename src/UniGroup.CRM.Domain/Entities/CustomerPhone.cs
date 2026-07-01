using System;

namespace UniGroup.CRM.Domain.Entities;

/// <summary>
/// Represents a phone number associated with a customer.
/// </summary>
public class CustomerPhone
{
    /// <summary>
    /// Gets or sets the phone number identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the customer this phone number belongs to.
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the customer navigation property.
    /// </summary>
    public virtual Customer Customer { get; set; } = null!;

    /// <summary>
    /// Gets or sets the phone number.
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this is the customer's primary phone number.
    /// </summary>
    public bool IsPrimary { get; set; }
}
