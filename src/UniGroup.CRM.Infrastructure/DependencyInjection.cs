using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Domain.Entities;
using UniGroup.CRM.Infrastructure.Data;
using UniGroup.CRM.Infrastructure.Services;

namespace UniGroup.CRM.Infrastructure;

/// <summary>
/// Extension methods for registering Infrastructure services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers database, identity, token generation services, and JWT authentication middleware configuration.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind JwtOptions
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));

        // Register Database Context.
        // Provider selection: defaults to SQL Server (production). Setting "Database:Provider" to
        // "Sqlite" enables a lightweight cross-platform provider for local/sandbox integration testing
        // where SQL Server is unavailable. Clean Architecture is preserved: only Infrastructure knows the provider.
        var databaseProvider = configuration.GetValue<string>("Database:Provider") ?? "SqlServer";
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (string.Equals(databaseProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(
                    configuration.GetConnectionString("SqliteConnection") ?? "Data Source=unigroup_crm_test.db",
                    b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
            }
            else
            {
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection"),
                    b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
            }
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Configure ASP.NET Core Identity
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // Register JWT Service Provider
        services.AddScoped<IJwtProvider, JwtProvider>();

        // Register Ticket and File Storage Services
        services.AddScoped<ITicketNumberGenerator, TicketNumberGenerator>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        // Register SLA Background Monitor
        services.AddHostedService<SlaMonitorService>();

        // ===== Phase 6: Bounded Channels (Inbox/Outbox patterns) =====
        services.AddSingleton<Channels.ChatwootWebhookChannel>();
        services.AddSingleton<Channels.AuditLogChannel>();

        // Phase 6: Webhook ingestion background consumer (idempotent, Polly retries)
        services.AddHostedService<BackgroundServices.ChatwootWebhookProcessor>();

        // Register HybridCache
#pragma warning disable EXTEXP0018
        services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new Microsoft.Extensions.Caching.Hybrid.HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(1),
                LocalCacheExpiration = TimeSpan.FromSeconds(30)
            };
        });
#pragma warning restore EXTEXP0018

        // Configure JWT Authentication
        var jwtSection = configuration.GetSection("Jwt");
        var secret = jwtSection.GetValue<string>("Secret");
        var issuer = jwtSection.GetValue<string>("Issuer");
        var audience = jwtSection.GetValue<string>("Audience");

        // Use safe fallbacks for initialization/compile verification if settings are not present during startup
        var key = Encoding.UTF8.GetBytes(string.IsNullOrEmpty(secret) ? "TemporarySecretKeyForCompilationPlaceholderLongerThan16Characters" : secret);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer ?? "UniGroupCRM",
                ValidAudience = audience ?? "UniGroupCRMAudience",
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.Zero
            };
        });

        return services;
    }
}
