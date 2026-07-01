using System;
using System.Collections.Generic;

namespace UniGroup.CRM.Domain.Entities;

/// <summary>
/// Represents a manufacturer brand of devices (e.g. Samsung, Apple).
/// </summary>
public class DeviceBrand
{
    /// <summary>
    /// Gets or sets the device brand identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the brand name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the models associated with this brand.
    /// </summary>
    public virtual ICollection<DeviceModel> DeviceModels { get; set; } = new List<DeviceModel>();
}
