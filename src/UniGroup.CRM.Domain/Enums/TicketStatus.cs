namespace UniGroup.CRM.Domain.Enums;

/// <summary>
/// Represents the life cycle status of a ticket.
/// </summary>
public enum TicketStatus
{
    /// <summary>
    /// A new ticket that has not been acknowledged or assigned yet.
    /// </summary>
    New = 0,

    /// <summary>
    /// The ticket is open and under initial review.
    /// </summary>
    Open = 1,

    /// <summary>
    /// An agent is actively working on the ticket.
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// The ticket is paused, waiting for information or response from the customer. Pauses SLA countdown.
    /// </summary>
    WaitingForCustomer = 3,

    /// <summary>
    /// The ticket is paused, waiting for spare parts or replacement stock. Pauses SLA countdown.
    /// </summary>
    WaitingForParts = 4,

    /// <summary>
    /// The ticket has breached its SLA or is escalated to higher tier support.
    /// </summary>
    Escalated = 5,

    /// <summary>
    /// The ticket issue is resolved, pending customer verification.
    /// </summary>
    Resolved = 6,

    /// <summary>
    /// The ticket is permanently closed.
    /// </summary>
    Closed = 7,

    /// <summary>
    /// The ticket was cancelled.
    /// </summary>
    Cancelled = 8
}
