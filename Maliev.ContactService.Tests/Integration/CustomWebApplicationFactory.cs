using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Tests.Testing;

namespace Maliev.ContactService.Tests.Integration;

public class CustomWebApplicationFactory<TProgram> : BaseIntegrationTestFactory<TProgram, ContactDbContext> where TProgram : class
{
    protected override void ConfigureEnvironmentVariables()
    {
        base.ConfigureEnvironmentVariables();
        // Enable permission-based auth in tests
        Environment.SetEnvironmentVariable("Features__PermissionBasedAuthEnabled", "true");
    }
}
