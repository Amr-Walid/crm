using System;
using System.Collections.Generic;

namespace UniGroup.CRM.Domain.Entities;

/// <summary>
/// Represents a customer in the CRM system.
/// </summary>
public class Customer
{
    /// <summary>
    /// Gets or sets the customer unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the customer's full name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the customer's email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the province (governorate) of the customer's address.
    /// </summary>
    public string? Province { get; set; }

    /// <summary>
    /// Gets or sets the city of the customer's address.
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// Gets or sets the detailed address description.
    /// </summary>
    public string? AddressDetails { get; set; }

    /// <summary>
    /// Gets or sets the customer group / segment (e.g. "VIP", "Retail").
    /// Optional free-text classification with a maximum length of 100 characters.
    /// </summary>
    public string? CustomerGroup { get; set; }

    /// <summary>
    /// Gets or sets when the customer profile was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the multiple phone numbers associated with this customer.
    /// </summary>
    public virtual ICollection<CustomerPhone> CustomerPhones { get; set; } = new List<CustomerPhone>();

    /// <summary>
    /// Gets or sets the devices owned by this customer.
    /// </summary>
    public virtual ICollection<CustomerDevice> CustomerDevices { get; set; } = new List<CustomerDevice>();

    /// <summary>
    /// Gets or sets the call records associated with this customer.
    /// </summary>
    public virtual ICollection<Call> Calls { get; set; } = new List<Call>();

    /// <summary>
    /// Gets or sets the tickets associated with this customer.
    /// </summary>
    public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();

    /// <summary>
    /// Gets or sets the preferred notification channels (e.g. "WhatsApp", "Email").
    /// Mapped as a primitive collection (JSON array column) in EF Core 9.
    /// </summary>
    public List<string> PreferredChannels { get; set; } = new List<string>();
}
