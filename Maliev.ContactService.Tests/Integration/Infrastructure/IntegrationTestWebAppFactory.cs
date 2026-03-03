using Maliev.ContactService.Application.Interfaces;
using Maliev.ContactService.Infrastructure.Persistence;
using Maliev.ContactService.Tests.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Xunit;

namespace Maliev.ContactService.Tests.Integration.Infrastructure;

public class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RabbitMqContainer _rabbitMqContainer;
    private readonly RedisContainer _redisContainer;
    private string? _connectionString;
    private string? _rabbitMqConnectionString;
    private string? _redisConnectionString;

    public IntegrationTestWebAppFactory()
    {
        Environment.SetEnvironmentVariable("CORS_ALLOWED_ORIGINS", "https://localhost:5001");

        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("contact_test")
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .Build();

        _rabbitMqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management-alpine")
            .Build();

        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        _postgresContainer.StartAsync().GetAwaiter().GetResult();
        _rabbitMqContainer.StartAsync().GetAwaiter().GetResult();
        _redisContainer.StartAsync().GetAwaiter().GetResult();

        _connectionString = _postgresContainer.GetConnectionString();
        _rabbitMqConnectionString = _rabbitMqContainer.GetConnectionString();
        _redisConnectionString = _redisContainer.GetConnectionString();
    }

    public string ConnectionString => _connectionString
        ?? throw new InvalidOperationException("Connection string not initialized");

    public string RabbitMqConnectionString => _rabbitMqConnectionString
        ?? throw new InvalidOperationException("RabbitMQ connection string not initialized");

    public string RedisConnectionString => _redisConnectionString
        ?? throw new InvalidOperationException("Redis connection string not initialized");

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        return DisposeAsync().AsTask();
    }

    public override async ValueTask DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
        await _rabbitMqContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ContactDbContext"] = ConnectionString,
                ["ConnectionStrings:rabbitmq"] = RabbitMqConnectionString,
                ["ConnectionStrings:redis"] = RedisConnectionString,
                ["CORS:AllowedOrigins:0"] = "https://localhost:5001"
            })
            .Build();

        builder.UseConfiguration(config);

        builder.ConfigureTestServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ContactDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<ContactDbContext>(options =>
            {
                options.UseNpgsql(ConnectionString);
            });

            using var scope = services.BuildServiceProvider().CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ContactDbContext>();
            context.Database.EnsureCreated();

            // Fix RowVersion default value issue with EnsureCreated
            context.Database.ExecuteSqlRaw(@"
                ALTER TABLE ""ContactMessages""
                ALTER COLUMN ""RowVersion"" SET DEFAULT '\\x00'::bytea;
            ");

            services.AddScoped<IUploadServiceClient, MockUploadServiceClient>();
            services.AddScoped<ICountryServiceClient, MockCountryServiceClient>();

            services.AddAuthentication(TestAuthHandler.AuthenticationScheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.AuthenticationScheme, options => { });
        });
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TestAuthHandler.AuthenticationScheme);
        return client;
    }
}

[CollectionDefinition(nameof(IntegrationTestCollection))]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestWebAppFactory>
{
}
