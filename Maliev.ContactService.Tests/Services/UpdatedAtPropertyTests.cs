using FluentAssertions;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.ContactService.Tests.Services;

public class UpdatedAtPropertyTests : IDisposable
{
    private readonly ContactDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly Mock<IUploadServiceClient> _uploadServiceMock;
    private readonly Mock<ILogger<Api.Services.ContactService>> _loggerMock;
    private readonly Api.Services.ContactService _contactService;

    public UpdatedAtPropertyTests()
    {
        var options = new DbContextOptionsBuilder<ContactDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ContactDbContext(options);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _uploadServiceMock = new Mock<IUploadServiceClient>();
        _loggerMock = new Mock<ILogger<Api.Services.ContactService>>();

        _contactService = new Api.Services.ContactService(_context, _cache, _uploadServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task UpdateContactStatusAsync_Should_Update_UpdatedAt_Property()
    {
        // Arrange
        var contact = new ContactMessage
        {
            FullName = "Test User",
            Email = "test@example.com",
            Subject = "Test Subject",
            Message = "Test Message",
            ContactType = ContactType.General,
            Priority = Priority.Medium,
            Status = ContactStatus.New,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        _context.ContactMessages.Add(contact);
        await _context.SaveChangesAsync();

        // Capture the original UpdatedAt value
        var originalUpdatedAt = contact.UpdatedAt;

        // Wait a bit to ensure there's a time difference
        await Task.Delay(100);

        var updateRequest = new UpdateContactStatusRequest
        {
            Status = ContactStatus.InProgress
        };

        // Act
        var result = await _contactService.UpdateContactStatusAsync(contact.Id, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ContactStatus.InProgress);
        result.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public async Task CreateContactMessageAsync_Should_Set_CreatedAt_And_UpdatedAt_To_Same_Value()
    {
        // Arrange
        var request = new CreateContactMessageRequest
        {
            FullName = "New User",
            Email = "newuser@example.com",
            Subject = "New Subject",
            Message = "New Message",
            ContactType = ContactType.General
        };

        // Act
        var result = await _contactService.CreateContactMessageAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.CreatedAt.Should().BeCloseTo(result.UpdatedAt, TimeSpan.FromSeconds(1));
    }

    public void Dispose()
    {
        _context?.Dispose();
        _cache?.Dispose();
    }
}