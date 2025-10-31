using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Tests.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.RateLimiting;

namespace Maliev.ContactService.Tests.Integration;

/// <summary>
/// Test factory specifically for rate limiting tests.
/// Uses RateLimitTesting environment with rate limiting enabled.
/// </summary>
public class RateLimitingTestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Use a custom environment that keeps rate limiting enabled
        builder.UseEnvironment("RateLimitTesting");

        // Skip DbContext configuration in Program.cs - we'll configure it here
        builder.UseSetting("UseTestcontainers", "false");

        // Configure test-specific rate limits (shorter windows, lower limits for faster tests)
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Configure fast rate limits for testing
                ["RateLimiting:FixedWindow:PermitLimit"] = "5",
                ["RateLimiting:FixedWindow:Window"] = "00:00:10", // 10 seconds
                ["RateLimiting:GlobalFixedWindow:PermitLimit"] = "20",
                ["RateLimiting:GlobalFixedWindow:Window"] = "00:00:10",
                // Skip DbContext registration in Program.cs
                ["UseTestcontainers"] = "false"
            });
        });

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

            // Use InMemory database for rate limiting tests
            services.AddDbContext<ContactDbContext>(options =>
            {
                options.UseInMemoryDatabase($"RateLimitTestDb_{Guid.NewGuid()}");
            });

            // Mock external services (not registered in RateLimitTesting environment)
            services.AddScoped<IUploadServiceClient, MockUploadServiceClient>();
            services.AddScoped<ICountryServiceClient, MockCountryServiceClient>();

            // Add test authentication
            services.AddAuthentication(TestAuthHandler.TestScheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.TestScheme, options => { });

            // DO NOT disable rate limiting - that's the point of these tests!
            // Rate limiting will be enabled from Program.cs for non-Testing environments
        });
    }
}
