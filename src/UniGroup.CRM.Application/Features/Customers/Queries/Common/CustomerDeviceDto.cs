using System;

namespace UniGroup.CRM.Application.Features.Customers.Queries.Common;

/// <summary>
/// Data transfer object representing customer device assignments, including warranty calculations.
/// </summary>
public record CustomerDeviceDto(
    Guid Id,
    Guid ModelId,
    string ModelName,
    string BrandName,
    string? IMEI,
    string? SerialNumber,
    DateTime PurchaseDate,
    string? InvoiceNumber,
    DateTime WarrantyExpiry,
    string WarrantyStatus
);
