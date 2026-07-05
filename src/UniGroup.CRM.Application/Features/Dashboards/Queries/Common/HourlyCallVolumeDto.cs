namespace UniGroup.CRM.Application.Features.Dashboards.Queries.Common;

/// <summary>
/// Data transfer object representing the hourly call volume metrics.
/// </summary>
public record HourlyCallVolumeDto(
    int Hour,
    int CallCount,
    double AvgDurationSeconds
);
