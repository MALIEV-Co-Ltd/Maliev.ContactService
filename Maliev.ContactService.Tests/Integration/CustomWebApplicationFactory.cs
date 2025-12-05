using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Tests.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ContactService.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory for integration tests.
/// Configures Testing environment with in-memory database and mock services.
/// </summary>
public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Skip DbContext configuration in Program.cs
        builder.UseSetting("UseTestcontainers", "false");

        builder.ConfigureServices(services =>
        {
            // Remove DbContext registrations from Program.cs if any
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

            // Use InMemory database for tests
            services.AddDbContext<ContactDbContext>(options =>
            {
                options.UseInMemoryDatabase($"ContactTestDb_{Guid.NewGuid()}");
            });

            // Remove the real IUploadServiceClient registration
            var uploadServiceDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IUploadServiceClient));

            if (uploadServiceDescriptor != null)
            {
                services.Remove(uploadServiceDescriptor);
            }

            // Add the mock implementation for IUploadServiceClient
            services.AddScoped<IUploadServiceClient, MockUploadServiceClient>();

            // Remove the real ICountryServiceClient registration
            var countryServiceDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ICountryServiceClient));

            if (countryServiceDescriptor != null)
            {
                services.Remove(countryServiceDescriptor);
            }

            // Add the mock implementation for ICountryServiceClient
            services.AddScoped<ICountryServiceClient, MockCountryServiceClient>();

            // Disable rate limiting for tests to avoid interference between test cases
            services.PostConfigure<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>(options =>
            {
                options.GlobalLimiter = null;
            });

            services.AddAuthentication(TestAuthHandler.TestScheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.TestScheme, options => {});
        });
    }
}
