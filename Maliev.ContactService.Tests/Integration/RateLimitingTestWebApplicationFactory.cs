using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Tests.Testing;
using Microsoft.AspNetCore.Hosting;
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set environment to "RateLimitTesting" BEFORE calling base
        // This ensures Program.cs sees the correct environment and enables rate limiting
        builder.UseEnvironment("RateLimitTesting");

        // Set JWT configuration for RateLimitTesting environment (Extensions.Authentication.cs needs this)
        var publicKeyPem = _testRsa.ExportRSAPublicKeyPem();
        var publicKeyBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(publicKeyPem));
        builder.UseSetting("Jwt:PublicKey", publicKeyBase64);
        builder.UseSetting("Jwt:Issuer", "test-issuer");
        builder.UseSetting("Jwt:Audience", "test-audience");

        // Call base to configure TestContainers and mock services
        // Note: base will call ConfigureTestServices which uses PostConfigureAll to override JWT settings
        base.ConfigureWebHost(builder);

        // Configure rate limiting settings
        builder.UseSetting("RateLimiting:FixedWindow:PermitLimit", "5");
        builder.UseSetting("RateLimiting:FixedWindow:Window", "00:00:10");
        builder.UseSetting("RateLimiting:GlobalFixedWindow:PermitLimit", "20");
        builder.UseSetting("RateLimiting:GlobalFixedWindow:Window", "00:00:10");
    }

    public new async Task DisposeAsync()
    {
        _testRsa?.Dispose();
        await base.DisposeAsync();
    }
}