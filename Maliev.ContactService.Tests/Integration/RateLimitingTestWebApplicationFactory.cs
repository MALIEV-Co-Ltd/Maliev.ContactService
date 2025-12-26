using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Tests.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Security.Cryptography;

namespace Maliev.ContactService.Tests.Integration;

/// <summary>
/// Test factory specifically for rate limiting tests.
/// Uses "RateLimitTesting" environment to enable rate limiting while maintaining all test infrastructure.
/// </summary>
public class RateLimitingTestWebApplicationFactory : BaseIntegrationTestFactory<Program, ContactDbContext>
{
    private readonly RSA _testRsa;

    public RateLimitingTestWebApplicationFactory()
    {
        _testRsa = RSA.Create(2048);
    }

    protected override string HostEnvironmentName => "RateLimitTesting";

    protected override void ConfigureEnvironmentVariables()
    {
        base.ConfigureEnvironmentVariables();
        // Enable permission-based auth in tests
        Environment.SetEnvironmentVariable("Features__PermissionBasedAuthEnabled", "true");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set JWT configuration for RateLimitTesting environment (Extensions.Authentication.cs needs this)
        var publicKeyPem = _testRsa.ExportRSAPublicKeyPem();
        var publicKeyBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(publicKeyPem));
        builder.ConfigureAppConfiguration((context, config) =>
        {
            var dict = new Dictionary<string, string?>
            {
                ["Jwt:PublicKey"] = publicKeyBase64,
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience"
            };
            config.AddInMemoryCollection(dict.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => (string?)kv.Value));
        });

        // Call base to configure TestContainers and mock services
        // Note: base will call ConfigureTestServices which uses PostConfigureAll to override JWT settings
        base.ConfigureWebHost(builder);

        // Configure rate limiting settings
        builder.UseSetting("AuditLog:Enabled", "false");
        builder.ConfigureAppConfiguration((context, config) =>
        {
            var dict = new Dictionary<string, string?>
            {
                ["RateLimiting:FixedWindow:PermitLimit"] = "5",
                ["RateLimiting:FixedWindow:Window"] = "00:00:10",
                ["RateLimiting:GlobalFixedWindow:PermitLimit"] = "20",
                ["RateLimiting:GlobalFixedWindow:Window"] = "00:00:10"
            };
            config.AddInMemoryCollection(dict.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => (string?)kv.Value));
        });
    }

    public new async Task DisposeAsync()
    {
        _testRsa?.Dispose();
        await base.DisposeAsync();
    }
}