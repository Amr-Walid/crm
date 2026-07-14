using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace UniGroup.CRM.Infrastructure.Data;

/// <summary>
/// Database context for the CRM application, inheriting from IdentityDbContext.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationDbContext"/> class.
    /// </summary>
    /// <param name="options">The options for configuring the context.</param>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the database set for refresh tokens.
    /// </summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// Gets or sets the database set for customers.
    /// </summary>
    public DbSet<Customer> Customers => Set<Customer>();

    /// <summary>
    /// Gets or sets the database set for customer phone numbers.
    /// </summary>
    public DbSet<CustomerPhone> CustomerPhones => Set<CustomerPhone>();

    /// <summary>
    /// Gets or sets the database set for device brands.
    /// </summary>
    public DbSet<DeviceBrand> DeviceBrands => Set<DeviceBrand>();

    /// <summary>
    /// Gets or sets the database set for device models.
    /// </summary>
    public DbSet<DeviceModel> DeviceModels => Set<DeviceModel>();

    /// <summary>
    /// Gets or sets the database set for customer devices.
    /// </summary>
    public DbSet<CustomerDevice> CustomerDevices => Set<CustomerDevice>();

    /// <summary>
    /// Gets or sets the database set for call records.
    /// </summary>
    public DbSet<Call> Calls => Set<Call>();

    /// <summary>
    /// Gets or sets the database set for departments.
    /// </summary>
    public DbSet<Department> Departments => Set<Department>();

    /// <summary>
    /// Gets or sets the database set for tickets.
    /// </summary>
    public DbSet<Ticket> Tickets => Set<Ticket>();

    /// <summary>
    /// Gets or sets the database set for ticket histories.
    /// </summary>
    public DbSet<TicketHistory> TicketHistories => Set<TicketHistory>();

    /// <summary>
    /// Gets or sets the database set for internal notes.
    /// </summary>
    public DbSet<InternalNote> InternalNotes => Set<InternalNote>();

    /// <summary>
    /// Gets or sets the database set for attachments.
    /// </summary>
    public DbSet<Attachment> Attachments => Set<Attachment>();

    /// <summary>
    /// Gets or sets the database set for audit trail entries (Phase 6).
    /// </summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>
    /// Gets or sets the database set for CSAT surveys (Phase 6).
    /// </summary>
    public DbSet<CsatSurvey> CsatSurveys => Set<CsatSurvey>();

    /// <summary>
    /// Gets or sets the database set for notification delivery logs (Phase 6).
    /// </summary>
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    /// <summary>
    /// Gets or sets the database set for processed webhook events (Phase 6 idempotency).
    /// </summary>
    public DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents => Set<ProcessedWebhookEvent>();

    /// <summary>
    /// Configures the model and table mappings.
    /// </summary>
    /// <param name="builder">The builder used to construct the database schema.</param>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Customize standard Identity tables to align with clean design naming conventions
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.LastName).HasMaxLength(100).IsRequired();

            // Optional relationship: each user optionally belongs to one department
            entity.HasOne(u => u.Department)
                .WithMany(d => d.Users)
                .HasForeignKey(u => u.DepartmentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ApplicationRole>(entity =>
        {
            entity.ToTable("Roles");
            entity.Property(r => r.Description).HasMaxLength(250);
        });

        // Configure RefreshToken entity mappings
        builder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Token).HasMaxLength(500).IsRequired();
            entity.Property(t => t.CreatedByIp).HasMaxLength(50).IsRequired();
            entity.Property(t => t.RevokedByIp).HasMaxLength(50);
            entity.Property(t => t.ReplacedByToken).HasMaxLength(500);

            entity.HasOne(t => t.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Customer entity mappings
        builder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).HasMaxLength(200).IsRequired();
            entity.Property(c => c.Email).HasMaxLength(150);
            entity.Property(c => c.Province).HasMaxLength(100);
            entity.Property(c => c.City).HasMaxLength(100);
            entity.Property(c => c.AddressDetails).HasMaxLength(500);
        });

        // Configure CustomerPhone entity mappings
        builder.Entity<CustomerPhone>(entity =>
        {
            entity.ToTable("CustomerPhones");
            entity.HasKey(cp => cp.Id);
            entity.Property(cp => cp.Phone).HasMaxLength(50).IsRequired();
            entity.HasIndex(cp => cp.Phone).IsUnique();

            entity.HasOne(cp => cp.Customer)
                .WithMany(c => c.CustomerPhones)
                .HasForeignKey(cp => cp.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure DeviceBrand entity mappings
        builder.Entity<DeviceBrand>(entity =>
        {
            entity.ToTable("DeviceBrands");
            entity.HasKey(db => db.Id);
            entity.Property(db => db.Name).HasMaxLength(100).IsRequired();
            // Unique index to prevent duplicate brand names at the DB level
            entity.HasIndex(db => db.Name).IsUnique();
        });

        // Configure DeviceModel entity mappings
        builder.Entity<DeviceModel>(entity =>
        {
            entity.ToTable("DeviceModels");
            entity.HasKey(dm => dm.Id);
            entity.Property(dm => dm.Name).HasMaxLength(150).IsRequired();
            // Composite unique index: same model name cannot appear twice under same brand
            entity.HasIndex(dm => new { dm.BrandId, dm.Name }).IsUnique();

            entity.HasOne(dm => dm.Brand)
                .WithMany(b => b.DeviceModels)
                .HasForeignKey(dm => dm.BrandId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure CustomerDevice entity mappings
        builder.Entity<CustomerDevice>(entity =>
        {
            entity.ToTable("CustomerDevices");
            entity.HasKey(cd => cd.Id);
            entity.Property(cd => cd.IMEI).HasMaxLength(100);
            entity.Property(cd => cd.SerialNumber).HasMaxLength(100);
            entity.Property(cd => cd.InvoiceNumber).HasMaxLength(100);

            // Filtered unique indexes to allow multiple null/empty values
            entity.HasIndex(cd => cd.IMEI)
                .IsUnique()
                .HasFilter("[IMEI] IS NOT NULL AND [IMEI] != ''");

            entity.HasIndex(cd => cd.SerialNumber)
                .IsUnique()
                .HasFilter("[SerialNumber] IS NOT NULL AND [SerialNumber] != ''");

            entity.HasOne(cd => cd.Customer)
                .WithMany(c => c.CustomerDevices)
                .HasForeignKey(cd => cd.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(cd => cd.Model)
                .WithMany(m => m.CustomerDevices)
                .HasForeignKey(cd => cd.ModelId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Call entity mappings
        builder.Entity<Call>(entity =>
        {
            entity.ToTable("Calls");
            entity.HasKey(c => c.Id);

            entity.Property(c => c.PhoneNumber).HasMaxLength(50).IsRequired();
            entity.Property(c => c.Summary).HasMaxLength(2000);
            entity.Property(c => c.RecordingUrl).HasMaxLength(500);
            entity.Property(c => c.TicketId).HasMaxLength(20);

            // Performance indexes for Caller ID lookup and reporting
            entity.HasIndex(c => c.PhoneNumber);
            entity.HasIndex(c => c.CustomerId);
            entity.HasIndex(c => c.AgentId);
            entity.HasIndex(c => c.TicketId);

            // Relationship with ApplicationUser (Agent) – restricted delete to preserve call history
            entity.HasOne(c => c.Agent)
                .WithMany(u => u.Calls)
                .HasForeignKey(c => c.AgentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Optional relationship with Customer – set null when customer is deleted
            entity.HasOne(c => c.Customer)
                .WithMany(cu => cu.Calls)
                .HasForeignKey(c => c.CustomerId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // Optional relationship with Ticket – set null when ticket is deleted
            entity.HasOne(c => c.Ticket)
                .WithMany()
                .HasForeignKey(c => c.TicketId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure Department entity mappings
        builder.Entity<Department>(entity =>
        {
            entity.ToTable("Departments");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Name).HasMaxLength(100).IsRequired();
            entity.Property(d => d.Description).HasMaxLength(300);
            entity.HasIndex(d => d.Name).IsUnique();
        });

        // Configure Ticket entity mappings
        builder.Entity<Ticket>(entity =>
        {
            entity.ToTable("Tickets");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Id).HasMaxLength(20).IsRequired();
            entity.Property(t => t.Title).HasMaxLength(200).IsRequired();
            entity.Property(t => t.Description).HasMaxLength(4000).IsRequired();
            entity.Property(t => t.ResolutionNote).HasMaxLength(2000);
            entity.Property(t => t.ChatwootConversationId).HasMaxLength(100);

            // Indexes for performance
            entity.HasIndex(t => t.Status);
            entity.HasIndex(t => t.Priority);
            entity.HasIndex(t => t.SlaDeadline);
            entity.HasIndex(t => t.CreatedAt);
            entity.HasIndex(t => t.AssignedToId);
            entity.HasIndex(t => t.CustomerId);

            // Relationships
            entity.HasOne(t => t.Customer)
                .WithMany(c => c.Tickets)
                .HasForeignKey(t => t.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.CustomerDevice)
                .WithMany()
                .HasForeignKey(t => t.CustomerDeviceId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(t => t.AssignedTo)
                .WithMany(u => u.AssignedTickets)
                .HasForeignKey(t => t.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Department)
                .WithMany(d => d.Tickets)
                .HasForeignKey(t => t.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure TicketHistory entity mappings
        builder.Entity<TicketHistory>(entity =>
        {
            entity.ToTable("TicketHistories");
            entity.HasKey(th => th.Id);
            entity.Property(th => th.TicketId).HasMaxLength(20).IsRequired();
            entity.Property(th => th.Note).HasMaxLength(1000);

            entity.HasOne(th => th.Ticket)
                .WithMany(t => t.Histories)
                .HasForeignKey(th => th.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(th => th.ChangedBy)
                .WithMany()
                .HasForeignKey(th => th.ChangedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure InternalNote entity mappings
        builder.Entity<InternalNote>(entity =>
        {
            entity.ToTable("InternalNotes");
            entity.HasKey(inote => inote.Id);
            entity.Property(inote => inote.TicketId).HasMaxLength(20).IsRequired();
            entity.Property(inote => inote.Content).HasMaxLength(3000).IsRequired();

            entity.HasOne(inote => inote.Ticket)
                .WithMany(t => t.InternalNotes)
                .HasForeignKey(inote => inote.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(inote => inote.Author)
                .WithMany()
                .HasForeignKey(inote => inote.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Attachment entity mappings
        builder.Entity<Attachment>(entity =>
        {
            entity.ToTable("Attachments");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.TicketId).HasMaxLength(20).IsRequired();
            entity.Property(a => a.FileName).HasMaxLength(255).IsRequired();
            entity.Property(a => a.StorageUrl).HasMaxLength(1000).IsRequired();
            entity.Property(a => a.ContentType).HasMaxLength(100).IsRequired();

            entity.HasOne(a => a.Ticket)
                .WithMany(t => t.Attachments)
                .HasForeignKey(a => a.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.UploadedBy)
                .WithMany()
                .HasForeignKey(a => a.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ===== Phase 6 Entities =====

        // Configure AuditLog entity mappings (Complex Type for ClientInfo)
        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Action).HasMaxLength(100).IsRequired();
            entity.Property(a => a.TableName).HasMaxLength(100).IsRequired();
            entity.Property(a => a.RecordId).HasMaxLength(100).IsRequired();

            // EF Core 9 Complex Type → columns ClientInfo_IpAddress / ClientInfo_UserAgent
            entity.ComplexProperty(a => a.ClientInfo, ci =>
            {
                ci.Property(c => c.IpAddress).HasMaxLength(100).HasColumnName("ClientInfo_IpAddress");
                ci.Property(c => c.UserAgent).HasColumnName("ClientInfo_UserAgent");
            });

            entity.HasIndex(a => a.CreatedAt);

            // Keep audit records when the user is removed
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure CsatSurvey entity mappings
        builder.Entity<CsatSurvey>(entity =>
        {
            entity.ToTable("CsatSurveys");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.TicketId).HasMaxLength(20).IsRequired();
            entity.Property(s => s.Feedback).HasMaxLength(1000);
            entity.Property(s => s.SurveyToken).HasMaxLength(450).IsRequired();

            // One survey per ticket + fast token lookup
            entity.HasIndex(s => s.TicketId).IsUnique();
            entity.HasIndex(s => s.SurveyToken).IsUnique();

            entity.HasOne(s => s.Ticket)
                .WithMany()
                .HasForeignKey(s => s.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cascade per database_design.md — no multiple cascade paths since Ticket→Customer is Restrict
            entity.HasOne(s => s.Customer)
                .WithMany()
                .HasForeignKey(s => s.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure NotificationLog entity mappings
        builder.Entity<NotificationLog>(entity =>
        {
            entity.ToTable("NotificationLogs");
            entity.HasKey(n => n.Id);
            entity.Property(n => n.RecipientType).HasMaxLength(100).IsRequired();
            entity.Property(n => n.RecipientId).HasMaxLength(100).IsRequired();
            entity.Property(n => n.Channel).HasMaxLength(100).IsRequired();
            entity.Property(n => n.TemplateType).HasMaxLength(100).IsRequired();
            entity.Property(n => n.Status).HasMaxLength(100).IsRequired();

            entity.HasIndex(n => n.SentAt);
        });

        // Configure ProcessedWebhookEvent entity mappings (idempotency inbox)
        builder.Entity<ProcessedWebhookEvent>(entity =>
        {
            entity.ToTable("ProcessedWebhookEvents");
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.EventId).HasMaxLength(450);
        });
    }
}
