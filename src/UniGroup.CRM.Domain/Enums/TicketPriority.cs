namespace UniGroup.CRM.Domain.Enums;

/// <summary>
/// Represents the ticket priorities and their associated SLA durations.
/// </summary>
public enum TicketPriority
{
    /// <summary>
    /// Low priority. SLA duration is 120 hours (5 days).
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium priority. SLA duration is 72 hours (3 days).
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High priority. SLA duration is 24 hours.
    /// </summary>
    High = 2,

    /// <summary>
    /// Critical priority. SLA duration is 4 hours.
    /// </summary>
    Critical = 3
}
