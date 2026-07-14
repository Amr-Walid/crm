using UniGroup.CRM.Application;
using UniGroup.CRM.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

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

        await db.SaveChangesAsync();
        Console.WriteLine("Seeding completed successfully.");
        return;
    }
}

app.MapControllers();

app.Run();
