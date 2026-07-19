using System;
using System.Collections.Generic;

namespace UniGroup.CRM.Application.Features.Customers.Queries.Common;

/// <summary>
/// Data transfer object containing full details of a customer, including phone numbers and devices.
/// </summary>
public record CustomerDetailsDto(
    Guid Id,
    string Name,
    string? Email,
    string? Province,
    string? City,
    string? AddressDetails,
    DateTime CreatedAt,
    ICollection<CustomerPhoneDto> Phones,
    ICollection<CustomerDeviceDto> Devices,
    string? CustomerGroup = null
);
