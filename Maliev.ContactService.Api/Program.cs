
using Maliev.ContactService.Api.Middleware;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Api.Services.Auth;
using Maliev.ContactService.Data.DbContexts;
using Maliev.Aspire.ServiceDefaults;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

using System.Threading.Channels;
using Maliev.ContactService.Data.Models;

var builder = WebApplication.CreateBuilder(args);

// --- Secrets & Configuration ---
builder.AddGoogleSecretManagerVolume(); // Load secrets from /mnt/secrets if available

// --- Infrastructure & Observability ---
builder.AddServiceDefaults(); // OpenTelemetry, health checks, resilience
builder.AddStandardMiddleware(options =>
{
    options.EnableRequestLogging = true;
});
builder.AddServiceMeters("contacts-meter", "Maliev.ContactService.Auth"); // Register service meters

// Add Redis, MassTransit, and PostgreSQL DbContext
// In testing, the test configuration provides Testcontainers connection strings
builder.AddRedisDistributedCache(instanceName: "contact:"); // Redis with in-memory fallback
builder.AddMassTransitWithRabbitMq(); // RabbitMQ message bus (non-blocking startup)
builder.AddPostgresDbContext<ContactDbContext>(connectionName: "ContactDbContext"); // PostgreSQL with retry logic

// --- API Configuration ---
builder.AddDefaultCors(); // CORS from CORS:AllowedOrigins config
builder.AddDefaultApiVersioning(); // API versioning with URL segment reader

// JWT Authentication (tests override via PostConfigureAll with dynamic RSA keys)
builder.AddJwtAuthentication();

// --- Authorization & Permissions ---
builder.Services.AddSingleton<IAuthMetrics, Maliev.ContactService.Api.Services.Auth.AuthMetricsService>();
builder.Services.AddPermissionAuthorization();

var auditLogEnabled = builder.Configuration.GetValue("AuditLog:Enabled", true);
if (auditLogEnabled)
{
    builder.Services.AddSingleton(System.Threading.Channels.Channel.CreateUnbounded<AuditLog>());
    builder.Services.AddHostedService<AuditLogBackgroundService>();
    builder.Services.AddSingleton<IAuditLogService, AuditLogService>();
}
else
{
    builder.Services.AddSingleton<IAuditLogService, NoOpAuditLogService>();
}

// Add OpenAPI (must be in Program.cs for XML comments to work via source generator)
if (!builder.Environment.IsProduction())
{
    builder.AddStandardOpenApi(
        title: "MALIEV Contact Service API",
        description: "Customer contact management service. Handles contact form submissions with file attachments, message status tracking (new/in-progress/resolved), and administrative tools for viewing, updating, and managing customer inquiries.");
}

// Configure FormOptions for multipart body length limit (250MB)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 262144000; // 250MB in bytes
});

// Configure Rate Limiting (skip in Testing environment)
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        var rateLimitingConfig = builder.Configuration.GetSection("RateLimiting");
        var fixedWindowOptions = rateLimitingConfig.GetSection("FixedWindow").Get<FixedWindowRateLimiterOptions>();
        var globalFixedWindowOptions = rateLimitingConfig.GetSection("GlobalFixedWindow").Get<FixedWindowRateLimiterOptions>();

        if (globalFixedWindowOptions != null)
        {
            options.AddPolicy("GlobalPolicy", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => globalFixedWindowOptions));
        }

        if (fixedWindowOptions != null)
        {
            options.AddPolicy("ContactPolicy", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => fixedWindowOptions));
        }
    });
}

// Configure UploadService HTTP client
builder.AddServiceClient<IUploadServiceClient, UploadServiceClient>("UploadService");

// Configure CountryService HTTP client
builder.AddServiceClient<ICountryServiceClient, CountryServiceClient>("CountryService");

// Configure IAM Service Client
builder.Services.AddIAMClient(builder.Configuration, "ContactService");

// IAM Registration Service
builder.Services.AddIAMRegistration<ContactIAMRegistrationService>();

// Register application services
builder.Services.AddScoped<IContactService, ContactService>();

builder.Services.AddControllers();

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

// Run database migrations on startup (skip in Testing environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    try
    {
        await app.MigrateDatabaseAsync<ContactDbContext>();
        
        // Seed authorization data
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ContactDbContext>();
        await DataSeeder.SeedAuthDataAsync(dbContext);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration or seeding failed - application may not function correctly");
        // Don't throw - allow app to start for debugging
    }
}

// Configure middleware pipeline
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStandardMiddleware();

if (!app.Environment.IsEnvironment("RateLimitTesting"))
{
    app.UseMiddleware<AuditLogMiddleware>();
}

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseRateLimiter();
}

app.UseCors();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map endpoints
app.MapControllers();

// Map Aspire default endpoints (/health, /alive, /metrics)
app.MapDefaultEndpoints(servicePrefix: "contact");

// Map OpenAPI and Scalar documentation (dev/staging only)
app.MapApiDocumentation(servicePrefix: "contact");

logger.LogInformation("ContactService started successfully");
await app.RunAsync();

/// <summary>
/// Main program class for the application
/// </summary>
public partial class Program { }
