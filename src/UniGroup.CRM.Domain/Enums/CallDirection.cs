namespace UniGroup.CRM.Domain.Enums;

/// <summary>
/// Represents the direction of a phone call relative to the call center.
/// </summary>
public enum CallDirection
{
    /// <summary>
    /// A call received from a customer to the call center.
    /// </summary>
    Inbound = 0,

    /// <summary>
    /// A call initiated by the call center agent toward the customer.
    /// </summary>
    Outbound = 1
}
