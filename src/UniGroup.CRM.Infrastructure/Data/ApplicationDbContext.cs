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
        });

        // Configure DeviceModel entity mappings
        builder.Entity<DeviceModel>(entity =>
        {
            entity.ToTable("DeviceModels");
            entity.HasKey(dm => dm.Id);
            entity.Property(dm => dm.Name).HasMaxLength(150).IsRequired();

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

            // Performance indexes for Caller ID lookup and reporting
            entity.HasIndex(c => c.PhoneNumber);
            entity.HasIndex(c => c.CustomerId);
            entity.HasIndex(c => c.AgentId);

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
        });
    }
}
