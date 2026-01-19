using Maliev.ContactService.Api.Services.Auth;
using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Maliev.ContactService.Tests.Services;

public class DataSeederTests : IClassFixture<CustomWebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private ContactDbContext _context = null!;

    public DataSeederTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        _context = _factory.CreateDbContext();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _factory.ResetDatabaseAsync();
    }

    [Fact]
    public async Task SeedAuthDataAsync_Should_Seed_Permissions_And_Roles()
    {
        // Act
        await DataSeeder.SeedAuthDataAsync(_context);

        // Assert
        var permissions = await _context.Permissions.ToListAsync();
        Assert.NotEmpty(permissions);

        var roles = await _context.Roles.Include(r => r.RolePermissions).ToListAsync();
        Assert.NotEmpty(roles);

        // Verify all predefined roles exist
        foreach (var roleDef in ContactPredefinedRoles.All)
        {
            var role = roles.FirstOrDefault(r => r.Name == roleDef.RoleId);
            Assert.NotNull(role);
            Assert.Equal(roleDef.Permissions.Count(), role.RolePermissions.Count);
        }
    }

    [Fact]
    public async Task SeedAuthDataAsync_Should_Be_Idempotent()
    {
        // Act
        await DataSeeder.SeedAuthDataAsync(_context);
        var firstCount = await _context.Permissions.CountAsync();

        await DataSeeder.SeedAuthDataAsync(_context);
        var secondCount = await _context.Permissions.CountAsync();

        // Assert
        Assert.Equal(firstCount, secondCount);
    }
}
