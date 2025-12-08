using System.Net;
using System.Net.Http.Json;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Data.Models;
using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Tests.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Testcontainers.PostgreSql;
using Xunit;

namespace Maliev.ContactService.Tests.Integration;

public class T055_RateLimitingTests : IClassFixture<T055_RateLimitingTests.SingleRateLimitTestWebAppFactory>, IAsyncLifetime
{
    private readonly SingleRateLimitTestWebAppFactory _factory;
    private HttpClient _client = null!;

    public T055_RateLimitingTests(SingleRateLimitTestWebAppFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await Task.CompletedTask;
    }
    
    [Fact]
    public async Task T055_Rate_Limiting_Should_Block_11th_Request_With_429()
    {
        var testEmail = $"ratelimit.{Guid.NewGuid():N}@example.com";
        var requests = new List<Task<HttpResponseMessage>>();

        for (int i = 0; i < 11; i++)
        {
            var request = new CreateContactMessageRequest
            {
                FullName = $"Rate Limit Test {i}",
                Email = $"{i}.{testEmail}",
                Subject = "Rate Limit Test",
                Message = "Testing rate limiting",
                CountryId = 1,
                ContactType = ContactType.General,
                Priority = Priority.Medium,
                Files = new List<CreateContactFileRequest>()
            };
            requests.Add(_client.PostAsJsonAsync("/contacts/v1/contacts", request));
        }

        var responses = await Task.WhenAll(requests);

        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.TooManyRequests);
        Assert.True(responses.Count(r => r.StatusCode == HttpStatusCode.Created) <= 10);
    }

    public class SingleRateLimitTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private PostgreSqlContainer? _postgresContainer;
        private string _connectionString = string.Empty;
        private readonly RSA _rsaKey;

        public SingleRateLimitTestWebAppFactory()
        {
            _rsaKey = RSA.Create(2048);
            EnsureContainerStarted();
        }

        private void EnsureContainerStarted()
        {
            if (_postgresContainer != null) return;

            _postgresContainer = new PostgreSqlBuilder()
                .WithImage("postgres:18")
                .WithDatabase("t055_rate_limit_db")
                .WithUsername("postgres")
                .WithPassword("test_password")
                .WithCleanUp(true)
                .Build();

            _postgresContainer.StartAsync().GetAwaiter().GetResult();
            _connectionString = _postgresContainer.GetConnectionString();
            ApplyMigrationsAsync().GetAwaiter().GetResult();
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public new async Task DisposeAsync()
        {
            if (_postgresContainer != null) await _postgresContainer.DisposeAsync();
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
            builder.UseSetting("RateLimiting:FixedWindow:PermitLimit", "10");
            builder.UseSetting("RateLimiting:FixedWindow:Window", "01:00:00");
            builder.UseSetting("Jwt:PublicKey", publicKeyBase64);

            builder.ConfigureServices(services =>
            {
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
}
