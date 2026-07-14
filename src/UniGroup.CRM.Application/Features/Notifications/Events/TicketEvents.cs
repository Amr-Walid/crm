using MediatR;
using System;

namespace UniGroup.CRM.Application.Features.Notifications.Events;

/// <summary>
/// Raised after a ticket is assigned to an agent (published post-SaveChanges).
/// </summary>
/// <param name="TicketId">The assigned ticket id.</param>
/// <param name="AssignedToId">The agent receiving the assignment.</param>
public record TicketAssignedEvent(string TicketId, Guid AssignedToId) : INotification;

/// <summary>
/// Raised after a ticket transitions to Resolved.
/// </summary>
/// <param name="TicketId">The resolved ticket id.</param>
/// <param name="CustomerId">The ticket's customer.</param>
/// <param name="ChatwootConversationId">Optional linked Chatwoot conversation.</param>
public record TicketResolvedEvent(string TicketId, Guid CustomerId, string? ChatwootConversationId) : INotification;

/// <summary>
/// Raised after a ticket transitions to Closed — triggers the CSAT loop.
/// </summary>
/// <param name="TicketId">The closed ticket id.</param>
/// <param name="CustomerId">The ticket's customer.</param>
/// <param name="ChatwootConversationId">Optional linked Chatwoot conversation.</param>
public record TicketClosedEvent(string TicketId, Guid CustomerId, string? ChatwootConversationId) : INotification;

/// <summary>
/// Raised when a ticket breaches its SLA deadline and is escalated.
/// </summary>
/// <param name="TicketId">The breaching ticket id.</param>
/// <param name="Title">The ticket title for the alert text.</param>
public record SlaBreachedEvent(string TicketId, string Title) : INotification;
