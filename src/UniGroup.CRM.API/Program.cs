using Microsoft.EntityFrameworkCore;
using UniGroup.CRM.Application;
using UniGroup.CRM.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// CORS for the Blazor WebAssembly client. Origins configurable via "Cors:AllowedOrigins";
// falls back to allowing any origin when unset (local sandbox / development usage).
const string ClientCorsPolicy = "ClientCors";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
builder.Services.AddCors(options =>
{
    options.AddPolicy(ClientCorsPolicy, policy =>
    {
        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    });
});

var app = builder.Build();

// When running on the SQLite test provider (no SQL Server available, e.g. Linux sandbox),
// create the schema directly from the EF model since the migrations are SQL Server-specific.
var configuredProvider = builder.Configuration.GetValue<string>("Database:Provider") ?? "SqlServer";
if (string.Equals(configuredProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
{
    using var startupScope = app.Services.CreateScope();
    var startupDb = startupScope.ServiceProvider.GetRequiredService<UniGroup.CRM.Infrastructure.Data.ApplicationDbContext>();
    startupDb.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(ClientCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

if (args.Contains("--seed"))
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<UniGroup.CRM.Infrastructure.Data.ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<UniGroup.CRM.Domain.Entities.ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<UniGroup.CRM.Domain.Entities.ApplicationRole>>();

        // 1. Create Roles
        var roles = new[] { "Admin", "Agent", "Team Leader", "User" };
        foreach (var r in roles)
        {
            if (!await roleManager.RoleExistsAsync(r))
            {
                await roleManager.CreateAsync(new UniGroup.CRM.Domain.Entities.ApplicationRole { Name = r, Description = $"{r} role" });
            }
        }

        // 2. Create Test User
        var userId = Guid.Parse("b8cc4a0f-c5f7-4f97-3916-08ded74840b4");
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            user = new UniGroup.CRM.Domain.Entities.ApplicationUser
            {
                Id = userId,
                UserName = "testuser@unigroup.com",
                Email = "testuser@unigroup.com",
                FirstName = "Test",
                LastName = "User",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            var createResult = await userManager.CreateAsync(user, "Password123!");
            if (createResult.Succeeded)
            {
                await userManager.AddToRolesAsync(user, roles);
            }
            else
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                Console.WriteLine($"Error creating user: {errors}");
            }
        }

        // 3. Create Customer
        var customerId = Guid.Parse("20df9317-2933-4260-bb53-7dc53ec14d64");
        var customer = await db.Customers.FindAsync(customerId);
        if (customer == null)
        {
            customer = new UniGroup.CRM.Domain.Entities.Customer
            {
                Id = customerId,
                Name = "Ahmed Walid",
                Email = "ahmed.walid@example.com",
                Province = "Cairo",
                City = "Maadi",
                AddressDetails = "Street 9, Building 10",
                CreatedAt = DateTime.UtcNow,
                PreferredChannels = new List<string> { "WhatsApp", "Email" }
            };
            db.Customers.Add(customer);
            db.CustomerPhones.Add(new UniGroup.CRM.Domain.Entities.CustomerPhone
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                Phone = "01012345678",
                IsPrimary = true
            });
        }

        // 4. Create Brand
        var brandId = Guid.Parse("4de75018-3517-407d-b33e-d9fcc66eafa8");
        var brand = await db.DeviceBrands.FindAsync(brandId);
        if (brand == null)
        {
            brand = new UniGroup.CRM.Domain.Entities.DeviceBrand
            {
                Id = brandId,
                Name = "Samsung"
            };
            db.DeviceBrands.Add(brand);
        }

        // 5. Create Model
        var modelId = Guid.Parse("5de75018-3517-407d-b33e-d9fcc66eafa8");
        var model = await db.DeviceModels.FindAsync(modelId);
        if (model == null)
        {
            model = new UniGroup.CRM.Domain.Entities.DeviceModel
            {
                Id = modelId,
                BrandId = brandId,
                Name = "Galaxy S24 Ultra"
            };
            db.DeviceModels.Add(model);
        }

        // 6. Create Customer Device
        var deviceId = Guid.Parse("feca8d9a-d8d9-4849-9de8-c2804ec36788");
        var device = await db.CustomerDevices.FindAsync(deviceId);
        if (device == null)
        {
            device = new UniGroup.CRM.Domain.Entities.CustomerDevice
            {
                Id = deviceId,
                CustomerId = customerId,
                ModelId = modelId,
                IMEI = "359876543210777",
                SerialNumber = "S24U987654777",
                PurchaseDate = DateTime.UtcNow.AddMonths(-6),
                InvoiceNumber = "INV-12345",
                WarrantyExpiry = DateTime.UtcNow.AddMonths(18)
            };
            db.CustomerDevices.Add(device);
        }

        // 7. Phase 6: Seed a closed ticket with an EXPIRED CSAT survey token (for expiration tests)
        const string expiredTicketId = "T-2026-90001";
        var expiredTicket = await db.Tickets.FindAsync(expiredTicketId);
        if (expiredTicket == null)
        {
            var closedAt = DateTime.UtcNow.AddDays(-10);
            expiredTicket = new UniGroup.CRM.Domain.Entities.Ticket
            {
                Id = expiredTicketId,
                CustomerId = customerId,
                Title = "Seeded closed ticket for expired CSAT test",
                Description = "Used to verify that expired survey tokens are rejected.",
                Category = UniGroup.CRM.Domain.Enums.TicketCategory.GeneralInquiry,
                Priority = UniGroup.CRM.Domain.Enums.TicketPriority.Low,
                Status = UniGroup.CRM.Domain.Enums.TicketStatus.Closed,
                CreatedAt = closedAt.AddDays(-1),
                UpdatedAt = closedAt,
                ClosedAt = closedAt
            };
            db.Tickets.Add(expiredTicket);

            db.CsatSurveys.Add(new UniGroup.CRM.Domain.Entities.CsatSurvey
            {
                Id = Guid.NewGuid(),
                TicketId = expiredTicketId,
                CustomerId = customerId,
                SurveyToken = "expiredtoken000000000000000000000000000000000000000000000000fixed",
                SentAt = closedAt,
                ExpiresAt = closedAt.AddDays(7) // already in the past (closed 10 days ago)
            });
        }

        // 8. Phase 7: Seed default Knowledge Base call-flow guidance articles.
        // Content is Markdown-formatted so the UI can render rich guidance during calls.
        var hasArticles = await db.KnowledgeBaseArticles.AnyAsync();
        if (!hasArticles)
        {
            db.KnowledgeBaseArticles.AddRange(
                new UniGroup.CRM.Domain.Entities.KnowledgeBaseArticle
                {
                    Id = Guid.NewGuid(),
                    Category = UniGroup.CRM.Domain.Enums.TicketCategory.ScreenDamage,
                    Title = "Screen Damage Troubleshooting & Intake Guidelines",
                    QuestionsToAsk =
                        "- Is the screen **physically cracked** or shattered?\n" +
                        "- Does the display show **lines, flickering, or black spots**?\n" +
                        "- Is **touch responsiveness** affected in any area of the screen?\n" +
                        "- Did the damage result from a **drop, pressure, or liquid contact**?",
                    DiagnosisSteps =
                        "1. Visually check for deep cracks or scratches on the glass.\n" +
                        "2. Verify the display **backlight** functionality (shine a light on a dark screen).\n" +
                        "3. Run the touch sensitivity calibration test in settings (`*#0*#` on Samsung).\n" +
                        "4. Ask the customer to draw across the whole screen to detect **dead touch zones**.",
                    SuggestedAnswers =
                        "> Your screen repair is handled under **out-of-warranty rates** unless accidental damage protection is active.\n" +
                        ">\n" +
                        "> The repair typically takes **1–2 hours** once the part is available at the service center.",
                    EscalationConditions =
                        "- Escalate to the **Hardware Maintenance Team** immediately for parts allocation.\n" +
                        "- If the device shows signs of **liquid damage**, escalate for a full board inspection.",
                    Keywords = "screen, display, cracked, shattered, broken glass, touch, lcd, oled, lines, black spots",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new UniGroup.CRM.Domain.Entities.KnowledgeBaseArticle
                {
                    Id = Guid.NewGuid(),
                    Category = UniGroup.CRM.Domain.Enums.TicketCategory.BatteryIssue,
                    Title = "Battery Drain & Power Intake Guidelines",
                    QuestionsToAsk =
                        "- Is the device using an **original charger** and cable?\n" +
                        "- How quickly does the battery drain from **100% to 0%**?\n" +
                        "- Does the device **overheat** during charging or normal use?\n" +
                        "- Is there any **swelling** visible on the back cover or screen edges?",
                    DiagnosisSteps =
                        "1. Inspect the **charging port** for debris or corrosion.\n" +
                        "2. Verify the **battery health percentage** in system settings.\n" +
                        "3. Monitor discharge rates under a high-performance load for 5 minutes.\n" +
                        "4. Check for background apps with abnormal battery consumption.",
                    SuggestedAnswers =
                        "> Battery replacement is covered **under warranty** if health is below **80%** within the first year.\n" +
                        ">\n" +
                        "> Otherwise, the replacement is chargeable at the standard out-of-warranty rate.",
                    EscalationConditions =
                        "- Escalate to the **Battery & Charging department** for a safety assessment if the battery is **swollen**.\n" +
                        "- **Never** advise the customer to charge a swollen device — treat it as a safety hazard.",
                    Keywords = "battery, drain, power, charging, overheat, swollen, health, percentage, charger",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            );
        }

        await db.SaveChangesAsync();
        Console.WriteLine("Seeding completed successfully.");
        return;
    }
}

app.MapControllers();

app.Run();
