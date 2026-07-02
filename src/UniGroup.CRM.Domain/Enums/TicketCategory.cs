namespace UniGroup.CRM.Domain.Enums;

/// <summary>
/// Represents the categories of issues for CRM tickets.
/// </summary>
public enum TicketCategory
{
    /// <summary>
    /// Screen or display damage.
    /// </summary>
    ScreenDamage = 0,

    /// <summary>
    /// Battery or power related issue.
    /// </summary>
    BatteryIssue = 1,

    /// <summary>
    /// Charging port or charging related issue.
    /// </summary>
    ChargingPort = 2,

    /// <summary>
    /// Software, OS, or application issue.
    /// </summary>
    SoftwareIssue = 3,

    /// <summary>
    /// Cellular network, Wi-Fi, or Bluetooth connectivity issue.
    /// </summary>
    NetworkConnectivity = 4,

    /// <summary>
    /// Front or rear camera issue.
    /// </summary>
    CameraIssue = 5,

    /// <summary>
    /// Speaker, microphone, or audio issue.
    /// </summary>
    SpeakerMicrophone = 6,

    /// <summary>
    /// Physical body, back glass, or frame damage.
    /// </summary>
    PhysicalDamage = 7,

    /// <summary>
    /// Inquiry about warranty terms or status.
    /// </summary>
    WarrantyInquiry = 8,

    /// <summary>
    /// General inquiries or customer support questions.
    /// </summary>
    GeneralInquiry = 9,

    /// <summary>
    /// Other uncategorized issues.
    /// </summary>
    Other = 99
}
