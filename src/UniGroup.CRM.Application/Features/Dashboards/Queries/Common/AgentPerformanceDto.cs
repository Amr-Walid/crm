using System;

namespace UniGroup.CRM.Application.Features.Dashboards.Queries.Common;

/// <summary>
/// Data transfer object representing performance metrics of an agent.
/// </summary>
public record AgentPerformanceDto(
    Guid AgentId,
    string AgentName,
    int TotalTicketsHandled,
    double AvgResolutionTimeHours,
    double SlaComplianceRate,
    double? CsatAvgScore,
    int OpenTicketsCount
);
