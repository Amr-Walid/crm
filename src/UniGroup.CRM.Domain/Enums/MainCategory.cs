namespace UniGroup.CRM.Domain.Enums;

/// <summary>
/// Represents the high-level (main) category of a ticket or classified call.
/// Each main category groups a set of related <see cref="TicketCategory"/>
/// sub-categories.
/// </summary>
public enum MainCategory
{
    /// <summary>
    /// Hardware / device maintenance and repair issues.
    /// </summary>
    Maintenance = 0,

    /// <summary>
    /// Customer complaints (software problems, dissatisfaction, other).
    /// </summary>
    Complaint = 1,

    /// <summary>
    /// General support, inquiries, and warranty questions.
    /// </summary>
    GeneralSupport = 2
}
