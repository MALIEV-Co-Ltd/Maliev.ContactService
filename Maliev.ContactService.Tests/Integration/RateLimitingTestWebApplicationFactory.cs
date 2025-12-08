using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Tests.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Distributed;
using Testcontainers.PostgreSql;

namespace Maliev.ContactService.Tests.Integration;

/// <summary>
/// Test factory specifically for rate limiting tests.
/// Uses a real PostgreSQL database via Testcontainers and enables rate limiting.
/// </summary>
public class RateLimitingTestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private string _connectionString = string.Empty;
    private readonly RSA _rsaKey;

    public RateLimitingTestWebApplicationFactory()
    {
        _rsaKey = RSA.Create(2048);
        EnsureContainerStarted();
    }

    private void EnsureContainerStarted()
    {
        if (_postgresContainer != null) return;

        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:18")
            .WithDatabase("rate_limit_test_db")
            .WithUsername("postgres")
            .WithPassword("test_password")
            .WithCleanUp(true)
            .Build();

        _postgresContainer.StartAsync().GetAwaiter().GetResult();
        _connectionString = _postgresContainer.GetConnectionString();
        ApplyMigrationsAsync().GetAwaiter().GetResult();
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public new async Task DisposeAsync()
    {
        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
        _rsaKey.Dispose();
        await base.DisposeAsync();
    }

    private async Task ApplyMigrationsAsync()
    {
        if (string.IsNullOrEmpty(_connectionString)) return;

        var optionsBuilder = new DbContextOptionsBuilder<ContactDbContext>();
        optionsBuilder.UseNpgsql(_connectionString);

        using var db = new ContactDbContext(optionsBuilder.Options);
        await db.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("RateLimitTesting");

        var publicKeyPem = _rsaKey.ExportRSAPublicKeyPem();
        var publicKeyBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(publicKeyPem));

        builder.UseSetting("UseTestcontainers", "true");
        builder.UseSetting("ConnectionStrings:ContactDbContext", "Host=localhost;Database=dummy;Username=dummy;Password=dummy");
        builder.UseSetting("RateLimiting:FixedWindow:PermitLimit", "5");
        builder.UseSetting("RateLimiting:FixedWindow:Window", "00:00:10");
        builder.UseSetting("RateLimiting:GlobalFixedWindow:PermitLimit", "20");
        builder.UseSetting("RateLimiting:GlobalFixedWindow:Window", "00:00:10");
        builder.UseSetting("Redis:Enabled", "false");
        builder.UseSetting("ConnectionStrings:redis", "localhost:6379");
        builder.UseSetting("ConnectionStrings:messaging", "amqp://guest:guest@localhost:5672");
        builder.UseSetting("Jwt:PublicKey", publicKeyBase64);

        builder.ConfigureServices(services =>
        {
            EnsureContainerStarted();
            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new InvalidOperationException("Testcontainers connection string is not initialized");
            }

            var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ContactDbContext>));
            if (dbContextDescriptor != null) services.Remove(dbContextDescriptor);

            var contextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ContactDbContext));
            if (contextDescriptor != null) services.Remove(contextDescriptor);

            var cacheDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDistributedCache));
            if (cacheDescriptor != null) services.Remove(cacheDescriptor);
            
            services.AddDbContext<ContactDbContext>(options => { options.UseNpgsql(_connectionString); });
            
            services.AddDistributedMemoryCache();

            services.AddScoped<IUploadServiceClient, MockUploadServiceClient>();
            services.AddScoped<ICountryServiceClient, MockCountryServiceClient>();

            services.AddAuthentication(TestAuthHandler.TestScheme)
                .AddScheme<TestAuthHandlerOptions, TestAuthHandler>(TestAuthHandler.TestScheme, options =>
                {
                    options.RsaKey = _rsaKey;
                });
        });
    }
}