using System;
using System.Collections.Generic;

namespace UniGroup.CRM.Domain.Entities;

/// <summary>
/// Represents a specific device model under a brand (e.g. Galaxy S24, iPhone 15).
/// </summary>
public class DeviceModel
{
    /// <summary>
    /// Gets or sets the device model identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the brand identifier this model belongs to.
    /// </summary>
    public Guid BrandId { get; set; }

    /// <summary>
    /// Gets or sets the brand navigation property.
    /// </summary>
    public virtual DeviceBrand Brand { get; set; } = null!;

    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the customer devices linked to this model.
    /// </summary>
    public virtual ICollection<CustomerDevice> CustomerDevices { get; set; } = new List<CustomerDevice>();
}
