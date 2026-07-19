namespace UniGroup.CRM.Application.Common.Models;

/// <summary>
/// Represents a single customer row parsed from an uploaded Excel import sheet.
/// Column names mirror the downloadable template header row.
/// </summary>
public class CustomerImportRow
{
    /// <summary>Gets or sets the customer's full name (required).</summary>
    public string? FullName { get; set; }

    /// <summary>Gets or sets the primary phone number (required, unique).</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>Gets or sets the optional email address.</summary>
    public string? Email { get; set; }

    /// <summary>Gets or sets the optional province / governorate.</summary>
    public string? Province { get; set; }

    /// <summary>Gets or sets the optional city.</summary>
    public string? City { get; set; }

    /// <summary>Gets or sets the optional detailed address.</summary>
    public string? AddressDetails { get; set; }

    /// <summary>Gets or sets the optional customer group / segment.</summary>
    public string? CustomerGroup { get; set; }
}
