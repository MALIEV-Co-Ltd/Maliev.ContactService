using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using HealthChecks.UI.Client;
using Maliev.ContactService.Api.HealthChecks;
using Maliev.ContactService.Api.Middleware;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Data.DbContexts;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using StackExchange.Redis;
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

    // Redis Distributed Cache Configuration
    var redisConnectionString = builder.Configuration["Redis:ConnectionString"];
    var redisEnabled = bool.TryParse(builder.Configuration["Redis:Enabled"], out var isRedisEnabled) && isRedisEnabled;

    if (redisEnabled && !string.IsNullOrEmpty(redisConnectionString) && !builder.Environment.IsEnvironment("Testing"))
    {
        try
        {
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "Contact:";
            });

            var redis = ConnectionMultiplexer.Connect(redisConnectionString);
            builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

            Log.Information("Redis distributed cache configured: {RedisConnectionString}", redisConnectionString);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Redis connection failed - will use in-memory cache fallback");
        }
    }
    else
    {
        Log.Information("Redis disabled or not configured - using in-memory cache only");
    }

    builder.Services.AddMemoryCache(); // Fallback in-memory cache

    // RabbitMQ Configuration (MassTransit)
    var rabbitmqHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
    var rabbitmqPort = int.TryParse(builder.Configuration["RabbitMQ:Port"], out var port) ? port : 5672;
    var rabbitmqUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
    var rabbitmqPassword = builder.Configuration["RabbitMQ:Password"] ?? "guest";
    var rabbitmqVhost = builder.Configuration["RabbitMQ:VirtualHost"] ?? "/";
    var rabbitmqEnabled = bool.TryParse(builder.Configuration["RabbitMQ:Enabled"], out var isRabbitmqEnabled) && isRabbitmqEnabled;

    if (rabbitmqEnabled && !builder.Environment.IsEnvironment("Testing"))
    {
        builder.Services.AddMassTransit(x =>
        {
            // Add consumers here if needed in the future
            // x.AddConsumer<SomeEventConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitmqHost, (ushort)rabbitmqPort, rabbitmqVhost, h =>
                {
                    h.Username(rabbitmqUser);
                    h.Password(rabbitmqPassword);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        Log.Information("MassTransit configured with RabbitMQ: {Host}:{Port}", rabbitmqHost, rabbitmqPort);
    }
    else
    {
        Log.Information("RabbitMQ/MassTransit disabled by configuration");
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

    // T039: Configure FormOptions for multipart body length limit (250MB)
    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 262144000; // 250MB in bytes
    });

    // Configure Contact DbContext
    // In Testing/RateLimitTesting environment, skip DbContext if using Testcontainers
    if (builder.Environment.IsEnvironment("Testing") || builder.Environment.IsEnvironment("RateLimitTesting"))
    {
        // Skip DbContext configuration if using Testcontainers
        // Testcontainers tests will configure DbContext in ConfigureWebHost
        var useTestcontainers = builder.Configuration.GetValue<bool>("UseTestcontainers");
        if (!useTestcontainers)
        {
            builder.Services.AddDbContext<ContactDbContext>(options =>
                options.UseInMemoryDatabase("TestDb"));
        }
    }
    else
    {
        builder.Services.AddDbContext<ContactDbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("ContactDbContext"));
        });
    }

    // Configure memory cache (simple configuration per CLAUDE.md standards)
    builder.Services.AddMemoryCache();

    // Configure rate limiting (skip in Testing environment)
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

    // Configure development fallbacks for UploadService
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.Configure<UploadServiceOptions>(options =>
        {
            options.BaseUrl = "http://localhost:8080";
            options.TimeoutInSeconds = 30;
        });
    }
    else
    {
        builder.Services.AddOptions<UploadServiceOptions>()
            .Bind(builder.Configuration.GetSection(UploadServiceOptions.SectionName))
            .ValidateDataAnnotations();
    }

    // Configure HTTP client for UploadService (skip in Testing - tests will provide mock)
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        builder.Services.AddHttpClient<IUploadServiceClient, UploadServiceClient>((serviceProvider, client) =>
        {
            var uploadServiceOptions = serviceProvider.GetRequiredService<IOptions<UploadServiceOptions>>().Value;
            client.BaseAddress = new Uri(uploadServiceOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(uploadServiceOptions.TimeoutInSeconds);
        });
    }

    // Configure CountryService options
    builder.Services.Configure<CountryServiceOptions>(builder.Configuration.GetSection(CountryServiceOptions.SectionName));

    // Configure development fallbacks for CountryService
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.Configure<CountryServiceOptions>(options =>
        {
            if (string.IsNullOrEmpty(options.BaseUrl))
            {
                options.BaseUrl = "http://localhost:8080";
            }
            options.TimeoutInSeconds = 10;
        });
    }

    // Configure HTTP client for CountryService (skip in Testing - tests will provide mock)
    // Note: Polly retry/circuit breaker policies would be added here if Polly package is installed
    // For now, relying on HttpClient timeout configured in CountryServiceClient
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        builder.Services.AddHttpClient<ICountryServiceClient, CountryServiceClient>();
    }

    // Register services
    builder.Services.AddScoped<IContactService, ContactService>();

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

    // Configure JWT Authentication with RSA public key validation (skip in Testing environment)
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
                        PublicKey = "default-key"
                    };
                    jwtSection.Bind(jwtOptions);

                    // Use RSA public key validation from shared config (maliev-shared-secrets)
                    var publicKeyBytes = Convert.FromBase64String(jwtOptions.PublicKey);
                    var publicKeyPem = Encoding.UTF8.GetString(publicKeyBytes);

                    // Import RSA public key from PEM format
                    var rsa = System.Security.Cryptography.RSA.Create();
                    rsa.ImportFromPem(publicKeyPem);

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidAudience = jwtOptions.Audience,
                        IssuerSigningKey = new RsaSecurityKey(rsa)
                    };

                    // Enhanced security options
                    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                    options.SaveToken = false;
                    options.IncludeErrorDetails = builder.Environment.IsDevelopment();

                    // Event handlers for security logging
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

            Log.Information("JWT Authentication configured with RSA public key validation");
        }
        else
        {
            Log.Warning("JWT configuration not found - API will start but authentication will not work. Configure JWT secrets for full functionality.");
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
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("10.0.0.0/8"));
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"));
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("192.168.0.0/16"));

            // Trust localhost for health checks and internal services
            options.KnownProxies.Add(System.Net.IPAddress.Loopback);
            options.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);
        }
        else if (builder.Environment.IsStaging())
        {
            // In staging, trust internal networks
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("10.0.0.0/8"));
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"));
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("192.168.0.0/16"));

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

    var healthChecksBuilder = builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("Database Health Check", tags: new[] { "readiness" });

    // Add Redis health check if enabled
    if (redisEnabled && !string.IsNullOrEmpty(redisConnectionString))
    {
        healthChecksBuilder.AddRedis(redisConnectionString, "redis", tags: new[] { "readiness" });
    }

    // Add service defaults for .NET Aspire
    builder.AddServiceDefaults();

    var app = builder.Build();

    // Configure base path for all routes
    app.UsePathBase("/contacts");

    app.UseForwardedHeaders();
    app.UseHttpsRedirection(); // Move HTTPS redirection to be called early

    // Configure the HTTP request pipeline
    // Enable Scalar API Documentation in non-production environments
    // For dev cluster: set ASPNETCORE_ENVIRONMENT=Development in deployment config
    if (!app.Environment.IsProduction())
    {
        app.MapOpenApi("/contacts/openapi/{documentName}.json");
        app.MapScalarApiReference("/contacts/scalar/v1", options =>
        {
            options
                .WithTitle("Maliev Contact Service API")
                .WithTheme(Scalar.AspNetCore.ScalarTheme.Saturn)
                .WithDefaultHttpClient(Scalar.AspNetCore.ScalarTarget.CSharp, Scalar.AspNetCore.ScalarClient.HttpClient)
                .WithOpenApiRoutePattern("/contacts/openapi/{documentName}.json");
        });

        Log.Information("Scalar API documentation enabled for {Environment} environment at /contacts/scalar/v1", app.Environment.EnvironmentName);
    }
    else
    {
        Log.Information("API documentation disabled in production environment for security");
    }

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    // app.UseHttpsRedirection(); // Remove from here - moved above

    // MANDATORY: Prometheus metrics
    if (!app.Environment.IsEnvironment("Testing"))
    {
        app.UseRateLimiter();
    }
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

    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public required string PublicKey { get; set; } // Base64-encoded RSA public key from shared config
}

// Make Program class accessible for integration tests
public partial class Program
{ }