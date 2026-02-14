using Maliev.ContactService.Data.DbContexts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Maliev.ContactService.Tests.Infrastructure;

public class ModelIntegrityTests
{
    [Fact]
    public void Model_ShouldNotHavePendingChanges()
    {
        var options = new DbContextOptionsBuilder<ContactDbContext>()
            .UseNpgsql("Host=localhost;Database=ModelCheck")
            .Options;

        using var context = new ContactDbContext(options);
        var hasChanges = context.Database.HasPendingModelChanges();

        Assert.False(hasChanges, "Run 'dotnet ef migrations add <Name> --project Maliev.ContactService.Data --startup-project Maliev.ContactService.Api'");
    }
}
