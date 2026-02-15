using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Tests.Testing;

namespace Maliev.ContactService.Tests.Integration;

public class RateLimitingTestWebApplicationFactory : BaseIntegrationTestFactory<Program, ContactDbContext>
{
    private readonly RSA _testRsa;

    public RateLimitingTestWebApplicationFactory() : base("RateLimitTesting")
    {
        _testRsa = RSA.Create(2048);

        // Match the expectations in RateLimitingIntegrationTests.cs:
        // 5 requests allowed, 6th should fail. Window 10 seconds.
        Environment.SetEnvironmentVariable("RateLimiting__PermitLimit", "5");
        Environment.SetEnvironmentVariable("RateLimiting__public__PermitLimit", "5");
        Environment.SetEnvironmentVariable("RateLimiting__Public__PermitLimit", "5");
        Environment.SetEnvironmentVariable("RateLimiting__FixedWindow__PermitLimit", "5");
        Environment.SetEnvironmentVariable("RateLimiting__WindowMinutes", "0.1666"); // approx 10 seconds if interpreted as fraction

        // Our AddStandardRateLimiting currently uses baseWindowMinutes = config.GetValue("RateLimiting:WindowMinutes", options.WindowMinutes);
        // And lo.Window = TimeSpan.FromMinutes(baseWindowMinutes);
        // 10 seconds = 10/60 minutes = 0.166666...
    }

    protected override void ConfigureEnvironmentVariables()
    {
        base.ConfigureEnvironmentVariables();

        var keyBytes = _testRsa.ExportSubjectPublicKeyInfo();
        Environment.SetEnvironmentVariable("Authentication__Jwt__PublicKey", Convert.ToBase64String(keyBytes));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
    }

    public new async Task DisposeAsync()
    {
        _testRsa?.Dispose();

        Environment.SetEnvironmentVariable("RateLimiting__PermitLimit", null);
        Environment.SetEnvironmentVariable("RateLimiting__public__PermitLimit", null);
        Environment.SetEnvironmentVariable("RateLimiting__Public__PermitLimit", null);
        Environment.SetEnvironmentVariable("RateLimiting__FixedWindow__PermitLimit", null);
        Environment.SetEnvironmentVariable("RateLimiting__WindowMinutes", null);

        await base.DisposeAsync();
    }
}
