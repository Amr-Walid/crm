using MediatR;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Domain.Entities;
using UniGroup.CRM.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace UniGroup.CRM.Application.Features.Calls.Commands.LogCall;

/// <summary>
/// Command to log a completed call record into the CRM system.
/// AgentId is NOT part of the request body – it is resolved from the authenticated JWT claim
/// by the controller and passed here, preventing identity spoofing.
/// </summary>
/// <param name="CustomerId">Optional customer ID. Null if the caller is not yet registered.</param>
/// <param name="AgentId">The ID of the authenticated agent, resolved from the JWT subject claim.</param>
/// <param name="Direction">Call direction: "Inbound" or "Outbound".</param>
/// <param name="PhoneNumber">The phone number involved in the call.</param>
/// <param name="DurationSeconds">The total duration of the call in seconds.</param>
/// <param name="Summary">Optional agent summary or notes after the call.</param>
/// <param name="RecordingUrl">Optional URL to the call recording on external storage.</param>
public record LogCallCommand(
    Guid? CustomerId,
    Guid AgentId,           // Injected from JWT by CallsController – never from request body
    string Direction,
    string PhoneNumber,
    int DurationSeconds,
    string? Summary,
    string? RecordingUrl
) : IRequest<Guid>;

/// <summary>
/// Handler for the <see cref="LogCallCommand"/>.
/// Validates the direction string, persists the call record, and returns the new call ID.
/// </summary>
public class LogCallCommandHandler : IRequestHandler<LogCallCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogCallCommandHandler"/> class.
    /// </summary>
    /// <param name="context">The application database context.</param>
    public LogCallCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Handles logging a call record.
    /// </summary>
    /// <param name="request">The command containing call details.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The <see cref="Guid"/> of the newly created call record.</returns>
    /// <exception cref="ArgumentException">Thrown when the Direction string is not a valid <see cref="CallDirection"/> value.</exception>
    public async Task<Guid> Handle(LogCallCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CallDirection>(request.Direction, ignoreCase: true, out var direction))
        {
            throw new ArgumentException(
                $"Invalid call direction '{request.Direction}'. Valid values are: Inbound, Outbound.");
        }

        var call = new Call
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            AgentId = request.AgentId,
            Direction = direction,
            PhoneNumber = request.PhoneNumber.Trim(),
            DurationSeconds = request.DurationSeconds,
            Summary = request.Summary,
            RecordingUrl = request.RecordingUrl,
            CreatedAt = DateTime.UtcNow
        };

        _context.Calls.Add(call);
        await _context.SaveChangesAsync(cancellationToken);

        return call.Id;
    }
}
