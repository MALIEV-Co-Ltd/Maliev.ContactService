using Maliev.ContactService.Data;
using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Maliev.ContactService.Tests.Models;

public class DataTests
{
    [Fact]
    public void ContactDbContextDesignTimeFactory_CreateDbContext_ReturnsContext()
    {
        // Arrange
        var factory = new ContactDbContextDesignTimeFactory();

        // Act
        var context = factory.CreateDbContext(Array.Empty<string>());

        // Assert
        Assert.NotNull(context);
    }

    [Fact]
    public void ContactDbContextDesignTimeFactory_CreateDbContext_WithEnvVar_ReturnsContext()
    {
        // Arrange
        var factory = new ContactDbContextDesignTimeFactory();
        var envVar = "ConnectionStrings__ContactDbContext";
        var original = Environment.GetEnvironmentVariable(envVar);
        try
        {
            Environment.SetEnvironmentVariable(envVar, "Host=localhost;Database=test");

            // Act
            var context = factory.CreateDbContext(Array.Empty<string>());

            // Assert
            Assert.NotNull(context);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, original);
        }
    }

    [Fact]
    public void ContactDbContext_Constructor_Options_Only_Works()
    {
        var options = new DbContextOptionsBuilder<ContactDbContext>()
            .UseInMemoryDatabase(databaseName: "OptionsOnly")
            .Options;
        using var context = new ContactDbContext(options);
        Assert.NotNull(context);
    }

    [Fact]
    public void Role_Properties_Work()
    {
        var id = Guid.NewGuid();
        var role = new Role { Id = id, Name = "Admin", Description = "Desc" };
        Assert.Equal(id, role.Id);
        Assert.Equal("Admin", role.Name);
        Assert.Equal("Desc", role.Description);
        Assert.NotNull(role.RolePermissions);
    }

    [Fact]
    public void Permission_Properties_Work()
    {
        var id = Guid.NewGuid();
        var permission = new Permission { Id = id, Name = "Read", Category = "Cat", Description = "Desc" };
        Assert.Equal(id, permission.Id);
        Assert.Equal("Read", permission.Name);
        Assert.Equal("Cat", permission.Category);
        Assert.Equal("Desc", permission.Description);
        Assert.NotNull(permission.RolePermissions);
    }

    [Fact]
    public void RolePermission_Properties_Work()
    {
        var roleId = Guid.NewGuid();
        var permId = Guid.NewGuid();
        var rp = new RolePermission { RoleId = roleId, PermissionId = permId };
        Assert.Equal(roleId, rp.RoleId);
        Assert.Equal(permId, rp.PermissionId);
    }

    [Fact]
    public void AuditLog_Properties_Work()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var log = new AuditLog
        {
            Id = id,
            UserId = "user",
            Action = "act",
            Resource = "res",
            Result = true,
            Reason = "none",
            ClientIp = "127.0.0.1",
            Timestamp = now
        };
        Assert.Equal(id, log.Id);
        Assert.Equal("user", log.UserId);
        Assert.Equal("act", log.Action);
        Assert.Equal("res", log.Resource);
        Assert.True(log.Result);
        Assert.Equal("none", log.Reason);
        Assert.Equal("127.0.0.1", log.ClientIp);
        Assert.Equal(now, log.Timestamp);
    }
}
