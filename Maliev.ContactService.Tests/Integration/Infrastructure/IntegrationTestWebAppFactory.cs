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

        _postgresContainer = 
#pragma warning disable CS0618
        new PostgreSqlBuilder()
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
#pragma warning restore CS0618

        builder.UseConfiguration(config);

        builder.ConfigureTestServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ContactDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            // Create DbContextOptions with explicit model building to avoid cached model issues
            var optionsBuilder = new DbContextOptionsBuilder<ContactDbContext>()
                .UseNpgsql(ConnectionString);

            // Use the options to get the correct model
            using (var context = new ContactDbContext(optionsBuilder.Options))
            {
                // Drop all tables first
                context.Database.ExecuteSqlRaw(@"
                    DROP TABLE IF EXISTS ""ContactFiles"" CASCADE;
                    DROP TABLE IF EXISTS ""ContactMessages"" CASCADE;
                    DROP TABLE IF EXISTS ""AuditLogs"" CASCADE;
                    DROP TABLE IF EXISTS ""RolePermissions"" CASCADE;
                    DROP TABLE IF EXISTS ""Roles"" CASCADE;
                    DROP TABLE IF EXISTS ""Permissions"" CASCADE;
                ");

                // Create tables matching the migration exactly - use raw SQL to avoid EF Core model caching issues
                // This creates the schema exactly as the migration would, including the xmin column
                context.Database.ExecuteSqlRaw(@"
                    CREATE TABLE ""AuditLogs"" (
                        ""Id"" uuid NOT NULL,
                        ""Timestamp"" timestamp with time zone NOT NULL,
                        ""UserId"" character varying(100) NOT NULL,
                        ""Action"" character varying(100) NOT NULL,
                        ""Resource"" character varying(200) NOT NULL,
                        ""Result"" boolean NOT NULL,
                        ""Reason"" character varying(500),
                        ""ClientIp"" character varying(50),
                        CONSTRAINT ""PK_AuditLogs"" PRIMARY KEY (""Id"")
                    );

                    CREATE TABLE ""Permissions"" (
                        ""Id"" uuid NOT NULL,
                        ""Name"" character varying(100) NOT NULL,
                        ""Description"" character varying(500),
                        ""Category"" character varying(100) NOT NULL,
                        CONSTRAINT ""PK_Permissions"" PRIMARY KEY (""Id"")
                    );

                    CREATE TABLE ""Roles"" (
                        ""Id"" uuid NOT NULL,
                        ""Name"" character varying(100) NOT NULL,
                        ""Description"" character varying(500),
                        CONSTRAINT ""PK_Roles"" PRIMARY KEY (""Id"")
                    );

                    CREATE TABLE ""ContactMessages"" (
                        ""Id"" integer NOT NULL GENERATED BY DEFAULT AS IDENTITY,
                        ""FullName"" character varying(200) NOT NULL,
                        ""Email"" character varying(254) NOT NULL,
                        ""PhoneNumber"" character varying(20),
                        ""Company"" character varying(200),
                        ""Subject"" character varying(500) NOT NULL,
                        ""Message"" text NOT NULL,
                        ""CountryId"" uuid NOT NULL,
                        ""ContactType"" integer NOT NULL,
                        ""Priority"" integer NOT NULL,
                        ""Status"" integer NOT NULL,
                        ""CreatedAt"" timestamp with time zone NOT NULL,
                        ""UpdatedAt"" timestamp with time zone NOT NULL,
                        ""ResolvedAt"" timestamp with time zone,
                        ""Xmin"" xid NULL,
                        CONSTRAINT ""PK_ContactMessages"" PRIMARY KEY (""Id"")
                    );

                    CREATE TABLE ""ContactFiles"" (
                        ""Id"" integer NOT NULL GENERATED BY DEFAULT AS IDENTITY,
                        ""ContactMessageId"" integer NOT NULL,
                        ""FileName"" character varying(255) NOT NULL,
                        ""ObjectName"" character varying(500) NOT NULL,
                        ""FileSize"" bigint,
                        ""ContentType"" character varying(100),
                        ""UploadServiceFileId"" character varying(100),
                        ""CreatedAt"" timestamp with time zone NOT NULL,
                        ""UpdatedAt"" timestamp with time zone NOT NULL,
                        CONSTRAINT ""PK_ContactFiles"" PRIMARY KEY (""Id""),
                        CONSTRAINT ""FK_ContactFiles_ContactMessages_ContactMessageId"" FOREIGN KEY (""ContactMessageId"") 
                            REFERENCES ""ContactMessages"" (""Id"") ON DELETE CASCADE
                    );

                    CREATE TABLE ""RolePermissions"" (
                        ""RoleId"" uuid NOT NULL,
                        ""PermissionId"" uuid NOT NULL,
                        CONSTRAINT ""PK_RolePermissions"" PRIMARY KEY (""RoleId"", ""PermissionId""),
                        CONSTRAINT ""FK_RolePermissions_Permissions_PermissionId"" FOREIGN KEY (""PermissionId"") 
                            REFERENCES ""Permissions"" (""Id"") ON DELETE CASCADE,
                        CONSTRAINT ""FK_RolePermissions_Roles_RoleId"" FOREIGN KEY (""RoleId"") 
                            REFERENCES ""Roles"" (""Id"") ON DELETE CASCADE
                    );

                    CREATE INDEX ""IX_ContactFiles_ContactMessageId"" ON ""ContactFiles"" (""ContactMessageId"");
                    CREATE INDEX ""IX_RolePermissions_PermissionId"" ON ""RolePermissions"" (""PermissionId"");
                    CREATE INDEX ""IX_ContactMessages_Email"" ON ""ContactMessages"" (""Email"");
                ");
            }

            services.AddDbContext<ContactDbContext>(options =>
            {
                options.UseNpgsql(ConnectionString);
            });

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



