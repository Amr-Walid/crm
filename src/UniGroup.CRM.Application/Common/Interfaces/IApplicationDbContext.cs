using Microsoft.EntityFrameworkCore;
using UniGroup.CRM.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace UniGroup.CRM.Application.Common.Interfaces;

/// <summary>
/// Interface for the application database context.
/// </summary>
public interface IApplicationDbContext
{
    /// <summary>
    /// Gets the database set for customers.
    /// </summary>
    DbSet<Customer> Customers { get; }

    /// <summary>
    /// Gets the database set for customer phone numbers.
    /// </summary>
    DbSet<CustomerPhone> CustomerPhones { get; }

    /// <summary>
    /// Gets the database set for device brands.
    /// </summary>
    DbSet<DeviceBrand> DeviceBrands { get; }

    /// <summary>
    /// Gets the database set for device models.
    /// </summary>
    DbSet<DeviceModel> DeviceModels { get; }

    /// <summary>
    /// Gets the database set for customer devices.
    /// </summary>
    DbSet<CustomerDevice> CustomerDevices { get; }

    /// <summary>
    /// Gets the database set for call records.
    /// </summary>
    DbSet<Call> Calls { get; }

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
