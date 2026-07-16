using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using UniGroup.CRM.Client;
using UniGroup.CRM.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// API base URL from wwwroot/appsettings.json (default: local backend).
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5112";

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

// Auth
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());

// App services
builder.Services.AddScoped<CrmApiClient>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<LanguageService>();
builder.Services.AddScoped<CallerIdService>();

// Chatwoot Dashboard-App embedding: single shared instance for the SPA session.
// EmbeddedStateService caches the ?embedded=true flag across navigations, and
// ChatwootContextService bridges the live conversation context from Chatwoot.
builder.Services.AddSingleton<EmbeddedStateService>();
builder.Services.AddSingleton<ChatwootContextService>();

await builder.Build().RunAsync();
