using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Data.Models;
using Maliev.ContactService.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.ContactService.Tests.Services;

public class UpdatedAtPropertyTests : IClassFixture<CustomWebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private ContactDbContext _context = null!;
    private Mock<IDistributedCache> _cacheMock = null!;
    private Mock<IUploadServiceClient> _uploadServiceMock = null!;
    private Mock<ICountryServiceClient> _countryServiceMock = null!;
    private Mock<ILogger<Api.Services.ContactService>> _loggerMock = null!;
    private Api.Services.ContactService _contactService = null!;

    public UpdatedAtPropertyTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        _context = _factory.CreateDbContext();
        _cacheMock = new Mock<IDistributedCache>();
        _uploadServiceMock = new Mock<IUploadServiceClient>();
        _countryServiceMock = new Mock<ICountryServiceClient>();
        _loggerMock = new Mock<ILogger<Api.Services.ContactService>>();

        _countryServiceMock.Setup(x => x.ValidateCountryExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _contactService = new Api.Services.ContactService(_context, _cacheMock.Object, _uploadServiceMock.Object, _countryServiceMock.Object, _loggerMock.Object);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _factory.ResetDatabaseAsync();
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
        Assert.NotNull(result);
        Assert.Equal(ContactStatus.InProgress, result.Status);
        Assert.True(result.UpdatedAt > originalUpdatedAt);
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
        Assert.NotNull(result);
        Assert.True((result.UpdatedAt - result.CreatedAt).Duration() < TimeSpan.FromSeconds(1));
    }
}
