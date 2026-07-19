using System.Collections.Generic;
using System.IO;
using System.Linq;
using MiniExcelLibs;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Common.Models;

namespace UniGroup.CRM.Infrastructure.Services;

/// <summary>
/// MiniExcel-backed implementation of <see cref="IExcelCustomerImportService"/>.
/// Reads and writes .xlsx files for the bulk customer import feature.
/// </summary>
public class ExcelCustomerImportService : IExcelCustomerImportService
{
    /// <summary>
    /// The ordered header columns of the import template.
    /// </summary>
    private static readonly string[] HeaderColumns =
    {
        "FullName", "PhoneNumber", "Email", "Province", "City", "AddressDetails", "CustomerGroup"
    };

    /// <inheritdoc />
    public byte[] GenerateTemplate()
    {
        // A single empty row keyed by the header columns produces a sheet whose
        // first row is exactly the expected header. Values are left blank so the
        // downloaded file is a clean template the user fills in.
        var templateRow = new Dictionary<string, object?>();
        foreach (var col in HeaderColumns)
        {
            templateRow[col] = null;
        }

        using var ms = new MemoryStream();
        // sheetName kept simple; printHeader true emits the dictionary keys as the header row.
        ms.SaveAs(new[] { templateRow }, printHeader: true, sheetName: "Customers");
        return ms.ToArray();
    }

    /// <inheritdoc />
    public List<CustomerImportRow> ParseRows(Stream stream)
    {
        // MiniExcel maps header cells to the property names of CustomerImportRow
        // (case-insensitive by default). Fully blank rows are discarded.
        var rows = stream.Query<CustomerImportRow>(sheetName: null)
            .Where(r =>
                !string.IsNullOrWhiteSpace(r.FullName) ||
                !string.IsNullOrWhiteSpace(r.PhoneNumber) ||
                !string.IsNullOrWhiteSpace(r.Email) ||
                !string.IsNullOrWhiteSpace(r.Province) ||
                !string.IsNullOrWhiteSpace(r.City) ||
                !string.IsNullOrWhiteSpace(r.AddressDetails) ||
                !string.IsNullOrWhiteSpace(r.CustomerGroup))
            .ToList();

        return rows;
    }
}
