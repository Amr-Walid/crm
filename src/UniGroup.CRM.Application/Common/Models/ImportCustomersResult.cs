using System.Collections.Generic;

namespace UniGroup.CRM.Application.Common.Models;

/// <summary>
/// Result summary returned after a bulk customer Excel import operation.
/// </summary>
/// <param name="SuccessCount">Number of customer rows successfully imported.</param>
/// <param name="FailedCount">Number of rows that were skipped or failed validation.</param>
/// <param name="Errors">Human-readable messages describing skipped/failed rows.</param>
public record ImportCustomersResult(
    int SuccessCount,
    int FailedCount,
    List<string> Errors
);
