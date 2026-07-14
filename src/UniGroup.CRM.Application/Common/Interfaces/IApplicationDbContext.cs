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
    /// Gets the database set for departments.
    /// </summary>
    DbSet<Department> Departments { get; }

    /// <summary>
    /// Gets the database set for tickets.
    /// </summary>
    DbSet<Ticket> Tickets { get; }

    /// <summary>
    /// Gets the database set for ticket histories.
    /// </summary>
    DbSet<TicketHistory> TicketHistories { get; }

    /// <summary>
    /// Gets the database set for internal notes.
    /// </summary>
    DbSet<InternalNote> InternalNotes { get; }

    /// <summary>
    /// Gets the database set for attachments.
    /// </summary>
    DbSet<Attachment> Attachments { get; }

    /// <summary>
    /// Gets the database set for audit trail entries (Phase 6).
    /// </summary>
    DbSet<AuditLog> AuditLogs { get; }

    /// <summary>
    /// Gets the database set for CSAT surveys (Phase 6).
    /// </summary>
    DbSet<CsatSurvey> CsatSurveys { get; }

    /// <summary>
    /// Gets the database set for notification delivery logs (Phase 6).
    /// </summary>
    DbSet<NotificationLog> NotificationLogs { get; }

    /// <summary>
    /// Gets the database set for processed webhook events (Phase 6 idempotency).
    /// </summary>
    DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents { get; }

    /// <summary>
    /// Gets the database set for knowledge base guidance articles (Phase 7).
    /// </summary>
    DbSet<KnowledgeBaseArticle> KnowledgeBaseArticles { get; }

    /// <summary>
    /// Gets the database set for application users (Identity).
    /// </summary>
    DbSet<ApplicationUser> Users { get; }

    /// <summary>
    /// Gets the database set for application roles (Identity).
    /// </summary>
    DbSet<ApplicationRole> Roles { get; }

    /// <summary>
    /// Gets the database set for user-role membership links (Identity).
    /// </summary>
    DbSet<Microsoft.AspNetCore.Identity.IdentityUserRole<System.Guid>> UserRoles { get; }

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
