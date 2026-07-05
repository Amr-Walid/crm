using MediatR;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Features.Dashboards.Queries.GetAgentPerformance;

namespace UniGroup.CRM.Application.Features.Reports.Queries.ExportAgentReport;

/// <summary>
/// Query to export agent performance report.
/// </summary>
public record ExportAgentReportQuery(
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    string Format = "CSV"
) : IRequest<ExportReportResult>;

/// <summary>
/// Represents the result of an export query containing the file stream and content details.
/// </summary>
public record ExportReportResult(Stream Content, string ContentType, string FileName);

/// <summary>
/// Handler for processing the ExportAgentReportQuery.
/// </summary>
public class ExportAgentReportQueryHandler : IRequestHandler<ExportAgentReportQuery, ExportReportResult>
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportAgentReportQueryHandler"/> class.
    /// </summary>
    public ExportAgentReportQueryHandler(ISender sender)
    {
        _sender = sender;
    }

    /// <inheritdoc />
    public async Task<ExportReportResult> Handle(ExportAgentReportQuery request, CancellationToken cancellationToken)
    {
        // Reuse the existing GetAgentPerformanceQuery which has built-in caching
        var performanceList = await _sender.Send(
            new GetAgentPerformanceQuery(request.DateFrom, request.DateTo, null),
            cancellationToken
        );

        var csvBuilder = new StringBuilder();
        
        // Write CSV header
        csvBuilder.AppendLine("AgentId,AgentName,TotalTicketsHandled,AvgResolutionTimeHours,SlaComplianceRatePercentage,CsatAvgScore,OpenTicketsCount");

        // Write CSV data rows
        foreach (var perf in performanceList)
        {
            csvBuilder.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "\"{0}\",\"{1}\",{2},{3:F2},{4:F2},{5},{6}",
                perf.AgentId,
                perf.AgentName.Replace("\"", "\"\""),
                perf.TotalTicketsHandled,
                perf.AvgResolutionTimeHours,
                perf.SlaComplianceRate,
                perf.CsatAvgScore.HasValue ? perf.CsatAvgScore.Value.ToString("F2", CultureInfo.InvariantCulture) : "N/A",
                perf.OpenTicketsCount
            ));
        }

        var bytes = Encoding.UTF8.GetBytes(csvBuilder.ToString());
        var memoryStream = new MemoryStream(bytes);

        var fileDate = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var fileName = $"AgentPerformanceReport_{fileDate}.csv";
        var contentType = "text/csv";

        return new ExportReportResult(memoryStream, contentType, fileName);
    }
}
