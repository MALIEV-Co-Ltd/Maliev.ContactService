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

    // Configure memory cache with size limit to prevent unbounded growth
    builder.Services.AddMemoryCache(options =>
    {
        options.SizeLimit = 1024; // Set a reasonable size limit in your preferred units
    });

    // Configure rate limiting
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

    // Configure CORS - Secure HTTPS-only policy
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(
            policy =>
            {
                // Production: HTTPS-only origins
                if (builder.Environment.IsProduction())
                {
                    policy.WithOrigins(
                        "https://maliev.com",
                        "https://www.maliev.com")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials(); // Enable credentials for authenticated requests
                }
                // Staging: HTTPS-only for staging domain
                else if (builder.Environment.IsStaging())
                {
                    policy.WithOrigins(
                        "https://staging.maliev.com",
                        "https://maliev-staging.web.app")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
                }
                // Development: Allow localhost with both HTTP and HTTPS
                else if (builder.Environment.IsDevelopment())
                {
                    policy.WithOrigins(
                        "https://localhost:3000",
                        "https://localhost:3001",
                        "http://localhost:3000",
                        "http://localhost:3001",
                        "https://maliev.com",
                        "https://www.maliev.com")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
                }
                // Default: Strict HTTPS-only
                else
                {
                    policy.WithOrigins("https://maliev.com")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
                }
            });

        // Add a named policy for API-only access (more restrictive)
        options.AddPolicy("ApiOnly", policy =>
        {
            policy.WithOrigins("https://maliev.com", "https://www.maliev.com")
                .WithHeaders("Content-Type", "Authorization", "X-API-Key")
                .WithMethods("GET", "POST", "PUT", "DELETE")
                .AllowCredentials();
        });
    });

    // Configure JWT Authentication (skip in Testing environment)
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
        if (jwtSection.Exists())
        {
            // Bind and validate JWT configuration
            var jwtOptions = new JwtOptions();
            jwtSection.Bind(jwtOptions);

            // Validate JWT configuration
            var missingValues = new List<string>();
            if (string.IsNullOrEmpty(jwtOptions.Issuer)) missingValues.Add("Issuer");
            if (string.IsNullOrEmpty(jwtOptions.Audience)) missingValues.Add("Audience");
            if (string.IsNullOrEmpty(jwtOptions.SecurityKey)) missingValues.Add("SecurityKey");

            if (missingValues.Count > 0)
            {
                Log.Error("JWT configuration validation failed. Missing required values: {MissingValues}", string.Join(", ", missingValues));
                throw new InvalidOperationException($"JWT configuration is incomplete. Missing: {string.Join(", ", missingValues)}");
            }

            // Validate security key strength
            if (jwtOptions.SecurityKey.Length < 32)
            {
                Log.Error("JWT SecurityKey must be at least 32 characters for security");
                throw new InvalidOperationException("JWT SecurityKey is too weak. Must be at least 32 characters.");
            }

            builder.Services.Configure<JwtOptions>(jwtSection);
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidAudience = jwtOptions.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecurityKey)),
                        ClockSkew = TimeSpan.FromMinutes(5), // Allow 5 minutes clock skew
                        RequireExpirationTime = true,
                        RequireSignedTokens = true
                    };

                    // Enhanced security options
                    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                    options.SaveToken = false; // Don't store tokens in AuthenticationProperties
                    options.IncludeErrorDetails = builder.Environment.IsDevelopment();

                    // Event handlers for better security logging
                    options.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            Log.Warning("JWT Authentication failed: {Error}", context.Exception.Message);
                            return Task.CompletedTask;
                        },
                        OnTokenValidated = context =>
                        {
                            Log.Debug("JWT Token validated for {User}", context.Principal?.Identity?.Name ?? "Unknown");
                            return Task.CompletedTask;
                        }
                    };
                });

            Log.Information("JWT Authentication configured with issuer: {Issuer}, audience: {Audience}",
                jwtOptions.Issuer, jwtOptions.Audience);
        }
        else
        {
            Log.Error("JWT configuration section '{SectionName}' not found - authentication will be disabled", JwtOptions.SectionName);
            throw new InvalidOperationException($"JWT configuration section '{JwtOptions.SectionName}' is required for authentication in {builder.Environment.EnvironmentName} environment");
        }
    }

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
        options.AddPolicy("UserOnly", policy => policy.RequireRole("User"));
    });

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        
        // Configure trusted proxies and networks based on environment
        if (builder.Environment.IsProduction())
        {
            // In production, trust internal Kubernetes networks
            // These are common Kubernetes service network ranges
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("10.0.0.0"), 8));
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("172.16.0.0"), 12));
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("192.168.0.0"), 16));
            
            // Trust localhost for health checks and internal services
            options.KnownProxies.Add(System.Net.IPAddress.Loopback);
            options.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);
        }
        else if (builder.Environment.IsStaging())
        {
            // In staging, trust internal networks
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("10.0.0.0"), 8));
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("172.16.0.0"), 12));
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("192.168.0.0"), 16));
            
            // Trust localhost for health checks and internal services
            options.KnownProxies.Add(System.Net.IPAddress.Loopback);
            options.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);
        }
        else
        {
            // In development/testing, trust localhost only for security
            options.KnownProxies.Add(System.Net.IPAddress.Loopback);
            options.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);
        }
    });

    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("Database Health Check", tags: new[] { "readiness" });

    var app = builder.Build();

    app.UseForwardedHeaders();
    app.UseHttpsRedirection(); // Move HTTPS redirection to be called early

    // Configure the HTTP request pipeline
    // Enable Swagger in non-production environments
    // For dev cluster: set ASPNETCORE_ENVIRONMENT=Development in deployment config
    if (!app.Environment.IsProduction())
    {
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

        Log.Information("Swagger UI enabled for {Environment} environment at /contacts/swagger", app.Environment.EnvironmentName);
    }
    else
    {
        Log.Information("Swagger UI disabled in production environment for security");
    }

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    // app.UseHttpsRedirection(); // Remove from here - moved above

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

    // Safe database initialization - only for non-production environments
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ContactDbContext>();
        try
        {
            // Only run migrations in Development or Testing environments
            if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
            {
                if (context.Database.IsRelational())
                {
                    context.Database.Migrate();
                    Log.Information("Database migration completed for {Environment}", app.Environment.EnvironmentName);
                }
                else
                {
                    context.Database.EnsureCreated();
                    Log.Information("In-memory database created for {Environment}", app.Environment.EnvironmentName);
                }
            }
            else
            {
                // Production: Only verify database connectivity
                if (context.Database.IsRelational())
                {
                    await context.Database.CanConnectAsync();
                    Log.Information("Database connectivity verified for production");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Database initialization failed for environment {Environment}", app.Environment.EnvironmentName);
            // In production, fail fast if database is not accessible
            if (app.Environment.IsProduction())
            {
                throw;
            }
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

    public string Issuer { get; set; } = null!;
    public string Audience { get; set; } = null!;
    public string SecurityKey { get; set; } = null!;
}

// Make Program class accessible for integration tests
public partial class Program
{ }