using Maliev.ContactService.Api.Exceptions;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Data.Models;
using Maliev.ContactService.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Maliev.ContactService.Tests.Services;

public class ContactServiceExtendedTests : IClassFixture<CustomWebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private ContactDbContext _context = null!;
    private Mock<IDistributedCache> _cacheMock = null!;
    private Mock<IUploadServiceClient> _uploadServiceMock = null!;
    private Mock<ICountryServiceClient> _countryServiceMock = null!;
    private Mock<ILogger<Api.Services.ContactService>> _loggerMock = null!;
    private Api.Services.ContactService _contactService = null!;

    public ContactServiceExtendedTests(CustomWebApplicationFactory<Program> factory)
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

        _contactService = new Api.Services.ContactService(_context, _cacheMock.Object, _uploadServiceMock.Object, _countryServiceMock.Object, _loggerMock.Object);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _factory.ResetDatabaseAsync();
    }

    [Fact]
    public async Task CreateContactMessageAsync_ShouldThrowDuplicateInquiryException_IfRecentExists()
    {
        // Arrange
        var email = "duplicate@example.com";
        var existing = new ContactMessage
        {
            Email = email,
            FullName = "First",
            Subject = "Sub",
            Message = "Msg",
            CountryId = Guid.Empty,
            CreatedAt = DateTimeOffset.UtcNow.AddSeconds(-30),
            UpdatedAt = DateTimeOffset.UtcNow.AddSeconds(-30)
        };
        _context.ContactMessages.Add(existing);
        await _context.SaveChangesAsync();

        var request = new CreateContactMessageRequest { Email = email, FullName = "Second", CountryId = Guid.Empty, Subject = "Sub", Message = "Msg" };

        // Act & Assert
        await Assert.ThrowsAsync<DuplicateInquiryException>(() => _contactService.CreateContactMessageAsync(request));
    }

    [Fact]
    public async Task CreateContactMessageAsync_ShouldThrowArgumentException_IfCountryInvalid()
    {
        // Arrange
        _countryServiceMock.Setup(x => x.ValidateCountryExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new CreateContactMessageRequest { Email = "test@example.com", FullName = "Test", CountryId = Guid.Empty, Subject = "Sub", Message = "Msg" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _contactService.CreateContactMessageAsync(request));
    }

    [Fact]
    public async Task GetContactFilesAsync_ShouldReturnFilesForContact()
    {
        // Arrange
        var contact = new ContactMessage { Email = "f@e.com", FullName = "N", Subject = "S", Message = "M", CountryId = Guid.Empty, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        _context.ContactMessages.Add(contact);
        await _context.SaveChangesAsync();

        var file = new ContactFile { ContactMessageId = contact.Id, FileName = "f.txt", ObjectName = "o", CreatedAt = DateTimeOffset.UtcNow };
        _context.ContactFiles.Add(file);
        await _context.SaveChangesAsync();

        // Act
        var result = await _contactService.GetContactFilesAsync(contact.Id);

        // Assert
        Assert.Single(result);
        Assert.Equal("f.txt", result.First().FileName);
    }

    [Fact]
    public async Task GetContactFileByIdAsync_ShouldReturnSpecificFile()
    {
        // Arrange
        var contact = new ContactMessage { Email = "f@e.com", FullName = "N", Subject = "S", Message = "M", CountryId = Guid.Empty, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        _context.ContactMessages.Add(contact);
        await _context.SaveChangesAsync();

        var file = new ContactFile { ContactMessageId = contact.Id, FileName = "f.txt", ObjectName = "o", CreatedAt = DateTimeOffset.UtcNow };
        _context.ContactFiles.Add(file);
        await _context.SaveChangesAsync();

        // Act
        var result = await _contactService.GetContactFileByIdAsync(contact.Id, file.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("f.txt", result!.FileName);
    }
}
