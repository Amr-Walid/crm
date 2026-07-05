namespace UniGroup.CRM.Application.Features.Dashboards.Queries.Common;

/// <summary>
/// Data transfer object representing device failure report metrics.
/// </summary>
public record DeviceFailureReportDto(
    string ModelName,
    string BrandName,
    int FailureCount,
    string? MostCommonCategory,
    int RepeatCustomersCount
);
