using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using HealthChecks.UI.Client;
using Maliev.ContactService.Api.Configurations;
using Maliev.ContactService.Api.HealthChecks;
using Maliev.ContactService.Api.Middleware;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Data.DbContexts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    Log.Information("Starting Maliev Contact Service");

    // Load secrets from mounted volume in GKE
    var secretsPath = "/mnt/secrets";
    if (Directory.Exists(secretsPath))
    {
        builder.Configuration.AddKeyPerFile(directoryPath: secretsPath, optional: true);
    }

    // API Versioning
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    }).AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    // Add controllers
    builder.Services.AddControllers();

    // Configure Contact DbContext
    if (builder.Environment.IsEnvironment("Testing"))
    {
        builder.Services.AddDbContext<ContactDbContext>(options =>
            options.UseInMemoryDatabase("TestDb"));
    }
    else
    {
        builder.Services.AddDbContext<ContactDbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("ContactDbContext"));
        });
    }

    // Configure memory cache (simple configuration without SizeLimit)
    builder.Services.AddMemoryCache();

    // Configure rate limiting
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Global rate limit
        options.AddPolicy("GlobalPolicy", context =>
            RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 1000,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 2,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 100
                }));

        // Contact form submission rate limit (more restrictive)
        options.AddPolicy("ContactPolicy", context =>
            RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 2,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 5
                }));
    });

    // Configure UploadService options
    builder.Services.Configure<UploadServiceOptions>(builder.Configuration.GetSection(UploadServiceOptions.SectionName));

    // Configure development fallbacks for UploadService
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.Configure<UploadServiceOptions>(options =>
        {
            options.BaseUrl = "http://localhost:8080";
            options.TimeoutSeconds = 30;
        });
    }
    else
    {
        builder.Services.AddOptions<UploadServiceOptions>()
            .Bind(builder.Configuration.GetSection(UploadServiceOptions.SectionName))
            .ValidateDataAnnotations();
    }

    // Configure HTTP client for UploadService
    builder.Services.AddHttpClient<IUploadServiceClient, UploadServiceClient>((serviceProvider, client) =>
    {
        var uploadServiceOptions = serviceProvider.GetRequiredService<IOptions<UploadServiceOptions>>().Value;
        client.BaseAddress = new Uri(uploadServiceOptions.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(uploadServiceOptions.TimeoutSeconds);
    });

    // Register services
    builder.Services.AddScoped<IContactService, ContactService>();

    // Configure Swagger
    builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
    builder.Services.AddSwaggerGen();

    // Configure CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(
            policy =>
            {
                policy.WithOrigins(
                    "https://maliev.com",
                    "https://*.maliev.com",
                    "http://maliev.com",
                    "http://*.maliev.com")
                .AllowAnyHeader()
                .AllowAnyMethod();
            });
    });

    // Configure JWT Authentication (skip in Testing environment)
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
        if (jwtSection.Exists())
        {
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    var jwtOptions = new JwtOptions
                    {
                        Issuer = "default-issuer",
                        Audience = "default-audience",
                        SecurityKey = "default-key"
                    };
                    jwtSection.Bind(jwtOptions);

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidAudience = jwtOptions.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecurityKey))
                    };
                });
        }
        else
        {
            // Log warning that JWT is not configured for local development
            Log.Warning("JWT configuration not found - API will start but authentication will not work. Configure JWT secrets for full functionality.");
        }
    }

    builder.Services.AddAuthorization();

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("Database Health Check", tags: new[] { "readiness" });

    var app = builder.Build();

    app.UseForwardedHeaders();

    // Configure the HTTP request pipeline
    app.UseSwagger(c =>
    {
        c.RouteTemplate = "contacts/swagger/{documentName}/swagger.json";
    });
    app.UseSwaggerUI(c =>
    {
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        foreach (var description in provider.ApiVersionDescriptions)
        {
            c.SwaggerEndpoint($"/contacts/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
        }
        c.RoutePrefix = "contacts/swagger";
    });

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseHttpsRedirection();

    // MANDATORY: Prometheus metrics
    app.UseHttpMetrics();
    app.UseRateLimiter();
    app.UseCors();

    // JWT Authentication & Authorization (only if configured and not in Testing environment)
    if (!app.Environment.IsEnvironment("Testing"))
    {
        var jwtSection = app.Configuration.GetSection(JwtOptions.SectionName);
        if (jwtSection.Exists())
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }
    }

    app.MapControllers();

    // Health check endpoints
    app.MapGet("/contacts/liveness", () => "Healthy").AllowAnonymous();

    app.MapHealthChecks("/contacts/readiness", new HealthCheckOptions
    {
        Predicate = healthCheck => healthCheck.Tags.Contains("readiness"),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    // MANDATORY: Prometheus metrics endpoint
    app.MapMetrics("/contacts/metrics");

    // Ensure database is created and seeded
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ContactDbContext>();
        try
        {
            if (context.Database.IsRelational())
            {
                context.Database.Migrate();
            }
            else
            {
                context.Database.EnsureCreated();
            }
            Log.Information("Database initialization completed");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while initializing the database");
        }
    }

    Log.Information("Maliev Contact Service started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public required string SecurityKey { get; set; }
}

// Make Program class accessible for integration tests
public partial class Program
{ }