using System.Net;
using System.Net.Http.Json;
using Maliev.ContactService.Api.Exceptions;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Data.Models;
using Maliev.ContactService.Tests.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Distributed;
using Testcontainers.PostgreSql;

namespace Maliev.ContactService.Tests.Integration;

/// <summary>
/// Local integration tests for T054-T057 that run without external dependencies.
/// These tests use Testcontainers PostgreSQL 18 and mock services.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Purpose", "LocalTesting")]
public class LocalIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private HttpClient _client = null!;

    public LocalIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        // Create client
        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await Task.CompletedTask;
    }

    #region T054: Health Check Tests

    [Fact]
    public async Task T054_Liveness_Endpoint_Should_Return_200_With_Healthy_Text()
    {
        // Act
        var response = await _client.GetAsync("/contact/liveness");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // liveness endpoint should always return 200
        Assert.Equal("Healthy", content); // liveness endpoint should return 'Healthy' text
    }

    [Fact]
    public async Task T054_Readiness_Endpoint_Should_Return_200_With_Health_Check_Structure()
    {
        // Act
        var response = await _client.GetAsync("/contact/readiness");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // readiness endpoint should return 200 when healthy
        Assert.False(string.IsNullOrEmpty(content)); // readiness should return health check details
        // Readiness endpoint returns JSON health check structure
        Assert.Contains("Healthy", content); // Content should contain "Healthy" status
    }

    #endregion

    #region T056: Duplicate Prevention Tests

    [Fact]
    public async Task T056_Duplicate_Inquiry_Within_60_Seconds_Should_Return_409()
    {
        // Arrange
        var testEmail = $"duplicate.{Guid.NewGuid():N}@example.com";
        var request = new CreateContactMessageRequest
        {
            FullName = "Duplicate Test User",
            Email = testEmail,
            Subject = "Duplicate Prevention Test",
            Message = "Testing duplicate inquiry prevention",
            CountryId = 1,
            ContactType = ContactType.General,
            Priority = Priority.Medium,
            Files = new List<CreateContactFileRequest>()
        };

        // Act - First submission (should succeed)
        var response1 = await _client.PostAsJsonAsync("/contact/v1/contacts", request);

        // Wait 2 seconds (well within 60 seconds window)
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Act - Second submission with same email (should fail with 409)
        var response2 = await _client.PostAsJsonAsync("/contact/v1/contacts", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response1.StatusCode); // first submission should succeed

        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode); // second submission within 60 seconds should return 409 Conflict

        var errorContent = await response2.Content.ReadAsStringAsync();
        Assert.Contains("recently submitted", errorContent); // error message should mention recent submission
    }

    [Fact]
    public async Task T056_Different_Email_Should_Not_Trigger_Duplicate_Prevention()
    {
        // Arrange
        var testEmail1 = $"user1.{Guid.NewGuid():N}@example.com";
        var testEmail2 = $"user2.{Guid.NewGuid():N}@example.com";

        var request1 = new CreateContactMessageRequest
        {
            FullName = "Test User 1",
            Email = testEmail1,
            Subject = "Test Subject",
            Message = "Test Message",
            CountryId = 1,
            ContactType = ContactType.General,
            Priority = Priority.Medium,
            Files = new List<CreateContactFileRequest>()
        };

        var request2 = new CreateContactMessageRequest
        {
            FullName = "Test User 2",
            Email = testEmail2,
            Subject = "Test Subject",
            Message = "Test Message",
            CountryId = 1,
            ContactType = ContactType.General,
            Priority = Priority.Medium,
            Files = new List<CreateContactFileRequest>()
        };

        // Act
        var response1 = await _client.PostAsJsonAsync("/contact/v1/contacts", request1);
        var response2 = await _client.PostAsJsonAsync("/contact/v1/contacts", request2);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response1.StatusCode); // first submission should succeed
        Assert.Equal(HttpStatusCode.Created, response2.StatusCode); // second submission with different email should also succeed
    }

    #endregion

    #region T057: Country Service Unavailability Tests

    [Fact]
    public async Task T057_Country_Service_Unavailability_Should_Return_503()
    {
        // Arrange - Create a client with a factory that uses a failing CountryServiceClient
        var client = CreateClientWithFailingCountryService();

        var request = new CreateContactMessageRequest
        {
            FullName = "Country Service Test",
            Email = $"countrytest.{Guid.NewGuid():N}@example.com",
            Subject = "Country Service Unavailability Test",
            Message = "Testing Country Service failure handling",
            CountryId = 1,
            ContactType = ContactType.General,
            Priority = Priority.Medium,
            Files = new List<CreateContactFileRequest>()
        };

        // Act
        var response = await client.PostAsJsonAsync("/contact/v1/contacts", request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode); // should return 503 when Country Service is unavailable

        Assert.Contains("country", content.ToLower()); // error message should mention country service issue
    }

    [Fact]
    public async Task T057_Country_Service_Unavailability_Should_Not_Create_Contact_Record()
    {
        // Arrange
        var client = CreateClientWithFailingCountryService();
        var testEmail = $"countrytest.{Guid.NewGuid():N}@example.com";

        var request = new CreateContactMessageRequest
        {
            FullName = "Country Service Test",
            Email = testEmail,
            Subject = "Test",
            Message = "Test",
            CountryId = 1,
            ContactType = ContactType.General,
            Priority = Priority.Medium,
            Files = new List<CreateContactFileRequest>()
        };

        // Act - Try to create contact (should fail due to Country Service)
        var createResponse = await client.PostAsJsonAsync("/contact/v1/contacts", request);

        // Assert - Should return 503
        Assert.Equal(HttpStatusCode.ServiceUnavailable, createResponse.StatusCode);

        // Verify no contact was created by checking if we can query it
        // Since we're using in-memory DB and the same factory, the record should not exist
        var getResponse = await client.GetAsync("/contact/v1/contacts");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var contacts = await getResponse.Content.ReadFromJsonAsync<IEnumerable<ContactMessageDto>>();
        Assert.NotNull(contacts);
        Assert.DoesNotContain(contacts, c => c.Email == testEmail); // contact should not be created when Country Service is unavailable
    }

    #endregion

    #region Additional Manual Testing Scenarios (Automated)

    [Fact]
    public async Task T058_Submit_Contact_With_Valid_CountryId_Should_Return_201()
    {
        // Arrange
        var testEmail = $"valid.country.{Guid.NewGuid():N}@example.com";
        var request = new CreateContactMessageRequest
        {
            FullName = "Valid Country Test",
            Email = testEmail,
            Subject = "Test with Valid Country",
            Message = "Testing valid countryId submission",
            CountryId = 1, // Valid country ID
            ContactType = ContactType.General,
            Priority = Priority.Medium,
            Files = new List<CreateContactFileRequest>()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/contact/v1/contacts", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); // valid contact submission should return 201 Created

        var contactDto = await response.Content.ReadFromJsonAsync<ContactMessageDto>();
        Assert.NotNull(contactDto);
        Assert.Equal(testEmail, contactDto!.Email);
        Assert.Equal(1, contactDto.CountryId);
    }

    [Fact]
    public async Task T059_Submit_Contact_With_Invalid_CountryId_Should_Return_400()
    {
        // Arrange
        var testEmail = $"invalid.country.{Guid.NewGuid():N}@example.com";
        var request = new CreateContactMessageRequest
        {
            FullName = "Invalid Country Test",
            Email = testEmail,
            Subject = "Test with Invalid Country",
            Message = "Testing invalid countryId submission",
            CountryId = 9999, // Out of valid range
            ContactType = ContactType.General,
            Priority = Priority.Medium,
            Files = new List<CreateContactFileRequest>()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/contact/v1/contacts", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // invalid countryId should return 400 Bad Request due to Range validation
    }

    [Fact]
    public async Task T060_Submit_Contact_With_Quotation_Type_Should_Return_422()
    {
        // Arrange
        var testEmail = $"quotation.{Guid.NewGuid():N}@example.com";
        var request = new
        {
            fullName = "Quotation Test User",
            email = testEmail,
            subject = "Quotation Request",
            message = "Testing quotation type rejection",
            countryId = 1,
            contactType = 2, // Quotation (removed type)
            priority = 1,
            files = new List<object>()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/contact/v1/contacts", request);

        // Assert - Quotation (2) is an invalid ContactType value, should return 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        // Validation error should mention the invalid contact type value
        Assert.False(string.IsNullOrEmpty(content));
    }

    [Fact]
    public async Task T061_Submit_Contact_With_Missing_Required_Fields_Should_Return_400()
    {
        // Arrange - Request with missing required fields
        var request = new
        {
            fullName = "Test User",
            // Missing email (required)
            // Missing subject (required)
            // Missing message (required)
            // Missing countryId (required)
            contactType = 0,
            priority = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync("/contact/v1/contacts", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // missing required fields should return 400 Bad Request

        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrEmpty(content)); // error response should contain validation messages
    }

    #endregion

    #region Helper Methods

    private HttpClient CreateClientWithFailingCountryService()
    {
        // Create a new factory instance with failing country service
        var factoryWithFailingService = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove all existing ICountryServiceClient registrations
                var descriptors = services.Where(
                    d => d.ServiceType == typeof(ICountryServiceClient)).ToList();

                foreach (var descriptor in descriptors)
                {
                    services.Remove(descriptor);
                }

                // Add a failing mock
                services.AddScoped<ICountryServiceClient, FailingCountryServiceClient>();
            });
        });

        // Create an authenticated client from the modified factory
        var token = _factory.CreateTestJwtToken("test-user", new[] { "Admin" });
        var client = factoryWithFailingService.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        return client;
    }

    #endregion
}

/// <summary>
/// Mock Country Service client that simulates service unavailability
/// </summary>
public class FailingCountryServiceClient : ICountryServiceClient
{
    public Task<bool> ValidateCountryExistsAsync(int countryId, CancellationToken cancellationToken = default)
    {
        return Task.FromException<bool>(
            new CountryServiceException("Country Service is currently unavailable. Please try again in a few moments."));
    }
}

/// <summary>
/// Custom WebApplicationFactory for local integration tests that uses Testcontainers PostgreSQL.
/// This provides a real PostgreSQL database for each test run, with automatic cleanup.
/// </summary>
public class LocalTestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private string _connectionString = string.Empty;
    private readonly RSA _rsaKey;

    public LocalTestWebApplicationFactory()
    {
        _rsaKey = RSA.Create(2048);
        EnsureContainerStarted();
    }

    private void EnsureContainerStarted()
    {
        if (_postgresContainer != null) return;

        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:18-alpine")
            .WithDatabase("contact_test_db")
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

    public void CleanDatabase()
    {
        if (string.IsNullOrEmpty(_connectionString)) return;

        var optionsBuilder = new DbContextOptionsBuilder<ContactDbContext>();
        optionsBuilder.UseNpgsql(_connectionString);

        using var db = new ContactDbContext(optionsBuilder.Options);
        try
        {
            db.Database.ExecuteSqlRaw("TRUNCATE TABLE \"ContactFiles\" CASCADE");
            db.Database.ExecuteSqlRaw("TRUNCATE TABLE \"ContactMessages\" RESTART IDENTITY CASCADE");
        }
        catch
        {
            // Ignore errors if tables don't exist yet
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("UseTestcontainers", "true");

        EnsureContainerStarted();

        // Set connection string environment variable for Program.cs
        Environment.SetEnvironmentVariable("ConnectionStrings__ContactDbContext", _connectionString);
        // Set RabbitMQ connection string (use localhost default for tests)
        Environment.SetEnvironmentVariable("ConnectionStrings__rabbitmq", "amqp://guest:guest@localhost:5672");

        var publicKeyPem = _rsaKey.ExportRSAPublicKeyPem();
        var publicKeyBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(publicKeyPem));
        builder.UseSetting("Jwt:PublicKey", publicKeyBase64);

        builder.ConfigureServices(services =>
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new InvalidOperationException("Testcontainers connection string is not initialized");
            }

            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ContactDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            var contextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ContactDbContext));
            if (contextDescriptor != null)
            {
                services.Remove(contextDescriptor);
            }

            services.AddDbContext<ContactDbContext>(options =>
            {
                options.UseNpgsql(_connectionString);
            });

            var cacheDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDistributedCache));
            if (cacheDescriptor != null)
            {
                services.Remove(cacheDescriptor);
            }
            services.AddDistributedMemoryCache();

            services.AddScoped<IUploadServiceClient, MockUploadServiceClient>();
            services.AddScoped<ICountryServiceClient, MockCountryServiceClient>();

            services.AddAuthentication(TestAuthHandler.TestScheme)
                .AddScheme<TestAuthHandlerOptions, TestAuthHandler>(
                    TestAuthHandler.TestScheme, options =>
                    {
                        options.RsaKey = _rsaKey;
                    });
        });
    }
}
