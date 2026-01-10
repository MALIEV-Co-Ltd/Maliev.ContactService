using System.IdentityModel.Tokens.Jwt;
using System.Diagnostics.CodeAnalysis;
using MassTransit;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Linq;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;


using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Xunit;
using Moq;
using Maliev.ContactService.Api.Services;

namespace Maliev.ContactService.Tests.Testing;

/// <summary>
/// Base integration test factory for ContactService.
/// Provides PostgreSQL, Redis, and RabbitMQ containers with parallel startup.
/// </summary>
/// <typeparam name="TProgram">The Program class of the service being tested</typeparam>
/// <typeparam name="TDbContext">The DbContext type for the service</typeparam>
public class BaseIntegrationTestFactory<TProgram, TDbContext> : WebApplicationFactory<TProgram>, IAsyncLifetime
    where TProgram : class
    where TDbContext : DbContext
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RedisContainer _redisContainer;
    private readonly RabbitMqContainer _rabbitmqContainer;
    private readonly RSA _testRsa;
    private bool _containersStarted;

    /// <summary>
    /// Override this property if your DbContext connection string has a different name.
    /// Defaults to the DbContext class name.
    /// </summary>
    protected virtual string DbConnectionStringName => typeof(TDbContext).Name;

    public BaseIntegrationTestFactory()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:18-alpine")
            .Build();

        _redisContainer = new RedisBuilder()
            .WithImage("redis:8.4-alpine")
            .Build();

        _rabbitmqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:4.2-alpine")
            .Build();

        _testRsa = RSA.Create(2048);
    }

    /// <summary>
    /// Override this to change the IHostEnvironment name used for the test host.
    /// Defaults to "Testing"; derived classes may override to use e.g. "RateLimitTesting".
    /// </summary>
    protected virtual string HostEnvironmentName => "Testing";

    public async Task InitializeAsync()
    {
        if (_containersStarted)
            return;

        // Start all containers in parallel
        await Task.WhenAll(
            _postgresContainer.StartAsync(),
            _redisContainer.StartAsync(),
            _rabbitmqContainer.StartAsync()
        );

        // Connection strings are injected into the test host's configuration via ConfigureWebHost

        // Wait for Redis to be ready
        using (var connection = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString()))
        {
            await connection.GetDatabase().PingAsync();
        }

        // Apply database migrations
        await ApplyMigrationsAsync();

        // Seed authorization data using a fresh context to avoid recursive loop via Services property
        await using (var dbContext = CreateDbContext())
        {
            if (dbContext is Maliev.ContactService.Data.DbContexts.ContactDbContext contactDbContext)
            {
                await Maliev.ContactService.Api.Services.Auth.DataSeeder.SeedAuthDataAsync(contactDbContext);
            }
        }

        _containersStarted = true;
    }

    public new async Task DisposeAsync()
    {
        // Stop background services first to avoid them trying to access databases during container shutdown
        try
        {
            var hostedServices = Services.GetServices<IHostedService>();
            foreach (var service in hostedServices)
            {
                if (service is Maliev.ContactService.Api.Services.Auth.AuditLogBackgroundService)
                {
                    await service.StopAsync(CancellationToken.None);
                }
            }
        }
        catch (ObjectDisposedException) { }

        await _postgresContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
        await _rabbitmqContainer.DisposeAsync();
        _testRsa.Dispose();
        await base.DisposeAsync();
    }


    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Ensure containers are started before creating host
        if (!_containersStarted)
        {
            InitializeAsync().GetAwaiter().GetResult();
        }

        // Ensure host runs in the desired test environment (can be overridden by derived classes)
        builder.UseEnvironment(HostEnvironmentName);

        // Also set the format expected by some Aspire helpers (raw base64 of public key info)
        var keyBytes = _testRsa.ExportSubjectPublicKeyInfo();
        Environment.SetEnvironmentVariable("Authentication__Jwt__PublicKey", Convert.ToBase64String(keyBytes));

        // Allow derived classes to set additional environment variables
        ConfigureEnvironmentVariables();

        // Inject configuration into the host builder early so Program.cs sees connection strings and other test settings during WebApplication.CreateBuilder
        builder.ConfigureHostConfiguration(configBuilder =>
        {
            var dict = new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DbConnectionStringName}"] = _postgresContainer.GetConnectionString(),
                ["ConnectionStrings:redis"] = _redisContainer.GetConnectionString(),
                ["ConnectionStrings:rabbitmq"] = _rabbitmqContainer.GetConnectionString(),
                ["ExternalServices:CountryService:BaseUrl"] = "http://localhost:5000",
                ["ExternalServices:UploadService:BaseUrl"] = "http://localhost:5001",
                ["Jwt:PublicKey"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(_testRsa.ExportRSAPublicKeyPem())),
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["UseTestcontainers"] = "true"
            };

            foreach (var kv in GetAdditionalConfiguration())
            {
                dict[kv.Key] = kv.Value;
            }

            configBuilder.AddInMemoryCollection(dict.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => (string?)kv.Value));
        });

        return base.CreateHost(builder);
    }

    protected virtual void ConfigureEnvironmentVariables() { }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {

        // Inject test-specific configuration scoped to the test host
        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            var dict = new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DbConnectionStringName}"] = _postgresContainer.GetConnectionString(),
                ["ConnectionStrings:redis"] = _redisContainer.GetConnectionString(),
                ["ConnectionStrings:rabbitmq"] = _rabbitmqContainer.GetConnectionString(),
                ["ExternalServices:CountryService:BaseUrl"] = "http://localhost:5000",
                ["ExternalServices:UploadService:BaseUrl"] = "http://localhost:5001",
                ["Jwt:PublicKey"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(_testRsa.ExportRSAPublicKeyPem())),
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["UseTestcontainers"] = "true",
                ["Service:Name"] = "ContactService",
                ["Service:Version"] = "1.0.0-test",
                ["Jwt:SecurityKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            };

            foreach (var kv in GetAdditionalConfiguration())
            {
                dict[kv.Key] = kv.Value;
            }

            configBuilder.AddInMemoryCollection(dict.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => (string?)kv.Value));
        });

        builder.ConfigureTestServices(services =>
        {
            // Configure JWT Bearer authentication with test RSA key
            services.PostConfigureAll<JwtBearerOptions>(options =>
            {
                // Disable claim type mapping to keep original claim names like "sub" instead of URIs
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = "test-issuer",
                    ValidAudience = "test-audience",
                    IssuerSigningKey = new RsaSecurityKey(_testRsa),
                    ClockSkew = TimeSpan.Zero, // No clock skew for tests
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
            });

            // Add MassTransit test harness for testing message publishing/consuming
            services.AddMassTransitTestHarness();

            // Allow derived classes to add additional test services
            ConfigureAdditionalServices(services);
        });
    }

    /// <summary>
    /// Override this method to supply additional in-memory configuration for the test host.
    /// Keys should be configuration keys (e.g., "ExternalServices:CountryService:BaseUrl").
    /// </summary>
    protected virtual IReadOnlyDictionary<string, string?> GetAdditionalConfiguration()
    {
        // Set dummy URLs for external services to prevent constructor injection failures
        Environment.SetEnvironmentVariable("CountryService__BaseUrl", "http://localhost:5000");
        Environment.SetEnvironmentVariable("UploadService__BaseUrl", "http://localhost:5001");

        return new Dictionary<string, string?>
        {
            ["ExternalServices:CountryService:BaseUrl"] = "http://localhost:5000",
            ["ExternalServices:UploadService:BaseUrl"] = "http://localhost:5001"
        };
    }

    /// <summary>
    /// Override this method to add additional test services to the DI container.
    /// </summary>
    protected virtual void ConfigureAdditionalServices(IServiceCollection services)
    {
        // Mock external services to prevent HTTP calls during tests

        // Mock CountryService
        var mockCountryService = new Mock<ICountryServiceClient>();
        mockCountryService.Setup(x => x.ValidateCountryExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        services.AddScoped(_ => mockCountryService.Object);

        // Mock UploadService
        var mockUploadService = new Mock<IUploadServiceClient>();
        mockUploadService.Setup(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
             .ReturnsAsync(new UploadResponse
             {
                 FileId = "test-file-id",
                 FileSize = 100,
                 ObjectName = "test-object",
                 Bucket = "test-bucket",
                 UploadedAt = DateTime.UtcNow
             });
        mockUploadService.Setup(x => x.DeleteFileAsync(It.IsAny<string>()))
             .ReturnsAsync(true);

        services.AddScoped(_ => mockUploadService.Object);
    }

    /// <summary>
    /// Gets the DbContext from the service provider for use in tests.
    /// </summary>
    public TDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TDbContext>();
    }

    /// <summary>
    /// Creates a new DbContext instance for testing (not from DI container).
    /// </summary>
    public TDbContext CreateDbContext()
    {
        var connectionString = _postgresContainer.GetConnectionString();
        var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return (TDbContext)Activator.CreateInstance(typeof(TDbContext), optionsBuilder.Options)!;
    }

    /// <summary>
    /// Applies all pending migrations to the test database.
    /// </summary>
    private async Task ApplyMigrationsAsync()
    {
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// Cleans all data from the database while preserving schema.
    /// Queries the database schema dynamically to get all tables.
    /// </summary>
    [SuppressMessage("Security", "EF1002:Gaps in SQL queries", Justification = "Table names are retrieved from information_schema and are safe.")]
    public async Task CleanDatabaseAsync()
    {
        await using var context = CreateDbContext();

        // Get all table names from information_schema
        var tableNames = await context.Database
            .SqlQueryRaw<string>(
                @"SELECT table_name
                  FROM information_schema.tables
                  WHERE table_schema = 'public'
                  AND table_type = 'BASE TABLE'
                  AND table_name != '__EFMigrationsHistory'
                  ORDER BY table_name")
            .ToListAsync();

        // Truncate all tables (CASCADE handles foreign keys)
        foreach (var tableName in tableNames)
        {
            try
            {
                await context.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE \"{tableName}\" RESTART IDENTITY CASCADE");
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P01")
            {
                // Table doesn't exist - ignore this error
            }
        }
    }

    /// <summary>
    /// Alias for CleanDatabaseAsync to support different naming conventions.
    /// </summary>
    public Task ResetDatabaseAsync() => CleanDatabaseAsync();

    /// <summary>
    /// Alias for CleanDatabaseAsync to support different naming conventions.
    /// </summary>
    public Task ClearDatabaseAsync() => CleanDatabaseAsync();

    /// <summary>
    /// Clears the in-memory cache.
    /// </summary>
    public void ClearCache()
    {
        // Get IMemoryCache from services and cast to MemoryCache to access Clear()
        var memoryCache = Services.GetService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
        if (memoryCache is Microsoft.Extensions.Caching.Memory.MemoryCache cache)
        {
            cache.Compact(1.0); // Compact 100% removes all entries
        }
    }

    /// <summary>
    /// Exposes the RSA signing credentials for JWT token creation in tests.
    /// </summary>
    public SigningCredentials SigningCredentials => new SigningCredentials(new RsaSecurityKey(_testRsa), SecurityAlgorithms.RsaSha256);

    /// <summary>
    /// Creates a test JWT token for authentication in integration tests.
    /// </summary>
    public string CreateTestJwtToken(
        string userId = "test-user",
        string[]? roles = null,
        Dictionary<string, string>? additionalClaims = null)
    {
        return CreateTestJwtToken(userId, roles, null, additionalClaims);
    }

    /// <summary>
    /// Creates a test JWT token with support for multi-value claims like permissions.
    /// </summary>
    public string CreateTestJwtToken(
        string userId,
        string[]? roles,
        string[]? permissions,
        Dictionary<string, string>? additionalClaims = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (roles != null)
        {
            foreach (var role in roles)
            {
                claims.Add(new Claim("role", role));
            }
        }

        if (permissions != null)
        {
            foreach (var permission in permissions)
            {
                claims.Add(new Claim("permissions", permission));
            }
        }

        if (additionalClaims != null)
        {
            foreach (var (key, value) in additionalClaims)
            {
                claims.Add(new Claim(key, value));
            }
        }

        var rsaSecurityKey = new RsaSecurityKey(_testRsa);
        var signingCredentials = new SigningCredentials(rsaSecurityKey, SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: signingCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Simplified JWT token generator with role parameter.
    /// Alias for CreateTestJwtToken to support different naming conventions.
    /// </summary>
    public string GenerateTestToken(string userId = "test-user", string role = "admin")
    {
        return CreateTestJwtToken(userId, new[] { role });
    }

    /// <summary>
    /// Creates an HTTP client with authenticated user and specified roles and permissions.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string userId = "test-user", string[]? roles = null, string[]? permissions = null)
    {
        var token = CreateTestJwtToken(userId, roles, permissions);
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
