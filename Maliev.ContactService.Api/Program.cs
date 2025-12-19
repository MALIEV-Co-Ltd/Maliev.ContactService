
using Maliev.ContactService.Api.Middleware;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Data.DbContexts;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// --- Secrets & Configuration ---
builder.AddGoogleSecretManagerVolume(); // Load secrets from /mnt/secrets if available

// --- Infrastructure & Observability ---
builder.AddServiceDefaults(); // OpenTelemetry, health checks, resilience
builder.AddServiceMeters("contacts-meter"); // Register service meters for OpenTelemetry business metrics

// Add Redis, MassTransit, and PostgreSQL DbContext
// In testing, the test configuration provides Testcontainers connection strings
builder.AddRedisDistributedCache(instanceName: "contact:"); // Redis with in-memory fallback
builder.AddMassTransitWithRabbitMq(); // RabbitMQ message bus (non-blocking startup)
builder.AddPostgresDbContext<ContactDbContext>(connectionStringName: "ContactDbContext"); // PostgreSQL with retry logic

// --- API Configuration ---
builder.AddDefaultCors(); // CORS from CORS:AllowedOrigins config
builder.AddDefaultApiVersioning(); // API versioning with URL segment reader

// JWT Authentication (tests override via PostConfigureAll with dynamic RSA keys)
builder.AddJwtAuthentication();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOnly", policy => policy.RequireRole("User"));
});

// Add OpenAPI (must be in Program.cs for XML comments to work via source generator)
if (!builder.Environment.IsProduction())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi("v1", options =>
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Info.Title = "MALIEV Contact Service API";
            document.Info.Version = "v1";
            document.Info.Description = "Customer contact management service. Handles contact form submissions with file attachments, message status tracking (new/in-progress/resolved), and administrative tools for viewing, updating, and managing customer inquiries.";
            return Task.CompletedTask;
        });
    });
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

// Configure UploadService options
builder.Services.Configure<UploadServiceOptions>(builder.Configuration.GetSection(UploadServiceOptions.SectionName));
builder.Services.AddHttpClient<IUploadServiceClient, UploadServiceClient>()
    .AddStandardResilienceHandler();

// Configure CountryService options and HTTP client
builder.Services.Configure<CountryServiceOptions>(builder.Configuration.GetSection(CountryServiceOptions.SectionName));
builder.Services.AddHttpClient<ICountryServiceClient, CountryServiceClient>()
    .AddStandardResilienceHandler();

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
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration failed - application may not function correctly");
        // Don't throw - allow app to start for debugging
    }
}

// Configure middleware pipeline
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseMiddleware<ExceptionHandlingMiddleware>();

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
