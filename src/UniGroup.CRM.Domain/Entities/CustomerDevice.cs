using System;

namespace UniGroup.CRM.Domain.Entities;

/// <summary>
/// Represents a device owned by a customer, along with its purchase and warranty details.
/// </summary>
public class CustomerDevice
{
    /// <summary>
    /// Gets or sets the customer device identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the customer who owns this device.
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the customer navigation property.
    /// </summary>
    public virtual Customer Customer { get; set; } = null!;

    /// <summary>
    /// Gets or sets the identifier of the device model.
    /// </summary>
    public Guid ModelId { get; set; }

    /// <summary>
    /// Gets or sets the device model navigation property.
    /// </summary>
    public virtual DeviceModel Model { get; set; } = null!;

    /// <summary>
    /// Gets or sets the International Mobile Equipment Identity (IMEI) of the device.
    /// </summary>
    public string? IMEI { get; set; }

    /// <summary>
    /// Gets or sets the serial number of the device.
    /// </summary>
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Gets or sets the purchase date of the device.
    /// </summary>
    public DateTime PurchaseDate { get; set; }

    /// <summary>
    /// Gets or sets the invoice number associated with the purchase.
    /// </summary>
    public string? InvoiceNumber { get; set; }

    /// <summary>
    /// Gets or sets the warranty expiration date.
    /// </summary>
    public DateTime WarrantyExpiry { get; set; }
}
