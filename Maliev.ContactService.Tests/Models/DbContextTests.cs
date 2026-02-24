using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Maliev.ContactService.Tests.Models;

public class DbContextTests
{
    [Fact]
    public async Task ContactDbContext_SaveChangesAsync_AddsTimestamps()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ContactDbContext>()
            .UseInMemoryDatabase(databaseName: "TimestampTest")
            .Options;

        using var context = new ContactDbContext(options);
        var message = new ContactMessage
        {
            FullName = "Test",
            Email = "test@example.com",
            Subject = "Test",
            Message = "Test"
        };

        // Act
        context.ContactMessages.Add(message);
        await context.SaveChangesAsync();

        // Assert
        Assert.NotEqual(DateTimeOffset.MinValue, message.CreatedAt);
        Assert.NotEqual(DateTimeOffset.MinValue, message.UpdatedAt);

        var originalUpdate = message.UpdatedAt;
        await Task.Delay(100);

        message.FullName = "Updated";
        context.ContactMessages.Update(message);
        await context.SaveChangesAsync();

        Assert.True(message.UpdatedAt > originalUpdate);
    }

    [Fact]
    public void ContactDbContext_SaveChanges_AddsTimestamps()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ContactDbContext>()
            .UseInMemoryDatabase(databaseName: "SyncTimestampTest")
            .Options;

        using var context = new ContactDbContext(options);
        var message = new ContactMessage
        {
            FullName = "Test",
            Email = "test@example.com",
            Subject = "Test",
            Message = "Test"
        };

        // Act
        context.ContactMessages.Add(message);
        context.SaveChanges();

        // Assert
        Assert.NotEqual(DateTimeOffset.MinValue, message.CreatedAt);
        Assert.NotEqual(DateTimeOffset.MinValue, message.UpdatedAt);
    }

    [Fact]
    public void ContactDbContext_OnModelCreating_Executes()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ContactDbContext>()
            .UseInMemoryDatabase(databaseName: "ModelTest")
            .Options;

        using var context = new ContactDbContext(options);

        // Act
        var model = context.Model;

        // Assert
        Assert.NotNull(model);
        var entityType = model.FindEntityType(typeof(ContactMessage));
        Assert.NotNull(entityType);
    }
}
