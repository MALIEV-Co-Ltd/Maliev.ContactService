using Maliev.Aspire.ServiceDefaults;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.ContactService.Api.Middleware;
using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Api.Services.Auth;
using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Data.Models;
// Initialize bootstrap logging
using var loggerFactory = LoggerFactory.Create(logBuilder => logBuilder.AddConsole());
var bootstrapLogger = loggerFactory.CreateLogger("Program");

try
{
    Maliev.ContactService.Api.Program.Log.StartingHost(bootstrapLogger, "Contact Service");

    var builder = WebApplication.CreateBuilder(args);

    // --- Secrets & Configuration ---
    builder.AddGoogleSecretManagerVolume(); // Load secrets from /mnt/secrets if available

    // --- Infrastructure & Observability ---
    builder.AddServiceDefaults(); // OpenTelemetry, health checks, resilience
    builder.AddStandardMiddleware(options =>
    {
        options.EnableRequestLogging = true;
    });
    builder.AddServiceMeters("contacts-meter"); // Register service meters

    // Add Redis, MassTransit, and PostgreSQL DbContext
    builder.AddStandardCache("contact:"); // Redis + in-memory fallback, memory-optimized // Redis with in-memory fallback
    builder.AddMassTransitWithRabbitMq(x =>
    {
        x.AddConsumer<Maliev.ContactService.Api.Consumers.FileDeletedEventConsumer>();
    }); // RabbitMQ message bus (non-blocking startup)
    builder.AddPostgresDbContext<ContactDbContext>(connectionName: "ContactDbContext"); // PostgreSQL with retry logic

    // --- API Configuration ---
    builder.AddStandardCors(); // CORS with fail-fast validation
    builder.AddDefaultApiVersioning(); // API versioning with URL segment reader

    // JWT Authentication (tests override via PostConfigureAll with dynamic RSA keys)
    builder.AddJwtAuthentication();

    // --- Authorization & Permissions ---
    builder.Services.AddSingleton<IAuthMetrics, Maliev.ContactService.Api.Services.Auth.AuthMetricsService>();
    builder.Services.AddPermissionAuthorization();

    // --- Audit Logging ---
    builder.Services.AddSingleton(System.Threading.Channels.Channel.CreateUnbounded<AuditLog>());
    builder.Services.AddHostedService<AuditLogBackgroundService>();
    builder.Services.AddSingleton<IAuditLogService, AuditLogService>();

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

    // Configure Rate Limiting
    builder.AddStandardRateLimiting(); // Memory-optimized for low-spec nodes
    // Configure UploadService HTTP client
    builder.AddServiceClient<IUploadServiceClient, UploadServiceClient>("UploadService");

    // Configure CountryService HTTP client
    builder.AddServiceClient<ICountryServiceClient, CountryServiceClient>("CountryService");

    // IAM Registration Service
    builder.AddIAMServiceClient("contact");
    builder.Services.AddIAMRegistration<ContactIAMRegistrationService>("contact");

    // Register application services
    builder.Services.AddScoped<IContactService, ContactService>();

    builder.Services.AddControllers();

    var app = builder.Build();
    var logger = app.Services.GetRequiredService<ILogger<Maliev.ContactService.Api.Program>>();

    // Run database migrations on startup
    await app.MigrateDatabaseAsync<ContactDbContext>();

    // Configure middleware pipeline

    app.UseForwardedHeaders();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseStandardMiddleware();

    app.UseMiddleware<AuditLogMiddleware>();
    app.UseRateLimiter();
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

    Maliev.ContactService.Api.Program.Log.ServiceStarted(logger, "Contact Service");
    await app.RunAsync();
}
catch (Exception ex)
{
    Maliev.ContactService.Api.Program.Log.HostTerminated(bootstrapLogger, ex, "Contact Service");
    throw;
}
finally
{
    loggerFactory.Dispose();
}

namespace Maliev.ContactService.Api
{
    /// <summary>
    /// Main program class for the application
    /// </summary>
    public partial class Program
    {
        internal static partial class Log
        {
            [LoggerMessage(Level = LogLevel.Information, Message = "Starting {ServiceName} host")]
            public static partial void StartingHost(ILogger logger, string serviceName);

            [LoggerMessage(Level = LogLevel.Critical, Message = "{ServiceName} host terminated unexpectedly during startup")]
            public static partial void HostTerminated(ILogger logger, Exception ex, string serviceName);

            [LoggerMessage(Level = LogLevel.Information, Message = "{ServiceName} started successfully")]
            public static partial void ServiceStarted(ILogger logger, string serviceName);
        }
    }
}
