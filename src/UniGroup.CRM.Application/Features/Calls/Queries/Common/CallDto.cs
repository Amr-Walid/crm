using System;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Application.Features.Calls.Queries.Common;

/// <summary>
/// Data transfer object representing a single call record for query responses.
/// </summary>
/// <param name="Id">The unique identifier of the call record.</param>
/// <param name="CustomerId">The optional customer ID linked to this call.</param>
/// <param name="CustomerName">The optional display name of the linked customer.</param>
/// <param name="AgentId">The identifier of the agent who handled the call.</param>
/// <param name="Direction">The call direction (Inbound or Outbound).</param>
/// <param name="PhoneNumber">The phone number involved in the call.</param>
/// <param name="DurationSeconds">The call duration in seconds.</param>
/// <param name="Summary">Optional agent notes or call summary.</param>
/// <param name="RecordingUrl">Optional URL to the call recording file.</param>
/// <param name="CreatedAt">The UTC timestamp when the call record was created.</param>
public record CallDto(
    Guid Id,
    Guid? CustomerId,
    string? CustomerName,
    Guid AgentId,
    CallDirection Direction,
    string PhoneNumber,
    int DurationSeconds,
    string? Summary,
    string? RecordingUrl,
    DateTime CreatedAt
);
