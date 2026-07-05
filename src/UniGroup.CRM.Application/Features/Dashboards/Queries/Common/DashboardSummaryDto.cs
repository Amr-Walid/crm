using System;

namespace UniGroup.CRM.Application.Features.Dashboards.Queries.Common;

/// <summary>
/// Data transfer object representing the real-time dashboard summary metrics.
/// </summary>
public record DashboardSummaryDto(
    int TodayNewTickets,
    int OpenTicketsTotal,
    int BreachedSlaCount,
    double AvgResolutionTimeToday,
    int CallsLoggedToday,
    string? TopIssueCategory,
    double SlaComplianceRate
);
