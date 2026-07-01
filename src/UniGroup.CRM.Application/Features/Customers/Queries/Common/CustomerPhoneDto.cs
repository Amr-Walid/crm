using System;

namespace UniGroup.CRM.Application.Features.Customers.Queries.Common;

/// <summary>
/// Data transfer object representing customer phone numbers.
/// </summary>
public record CustomerPhoneDto(
    Guid Id,
    string Phone,
    bool IsPrimary
);
