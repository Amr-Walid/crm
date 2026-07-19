using System.Collections.Generic;
using System.IO;
using UniGroup.CRM.Application.Common.Models;

namespace UniGroup.CRM.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the Excel (.xlsx) reader/writer used for the bulk
/// customer import feature. The concrete implementation lives in the
/// Infrastructure layer (MiniExcel) so the Application layer stays free of
/// third-party spreadsheet dependencies.
/// </summary>
public interface IExcelCustomerImportService
{
    /// <summary>
    /// Generates the blank import template (.xlsx) containing only the
    /// expected header row: FullName, PhoneNumber, Email, Province, City,
    /// AddressDetails, CustomerGroup.
    /// </summary>
    /// <returns>The raw bytes of the generated .xlsx template.</returns>
    byte[] GenerateTemplate();

    /// <summary>
    /// Parses the uploaded .xlsx stream into a list of strongly-typed rows.
    /// </summary>
    /// <param name="stream">The uploaded Excel file stream.</param>
    /// <returns>The parsed customer rows in sheet order.</returns>
    List<CustomerImportRow> ParseRows(Stream stream);
}
