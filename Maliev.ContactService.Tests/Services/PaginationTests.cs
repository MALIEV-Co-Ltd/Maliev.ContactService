using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Data.Models;
using Maliev.ContactService.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.ContactService.Tests.Services;

public class PaginationTests : IClassFixture<CustomWebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private ContactDbContext _context = null!;
    private Mock<IDistributedCache> _cacheMock = null!;
    private Mock<IUploadServiceClient> _uploadServiceMock = null!;
    private Mock<ICountryServiceClient> _countryServiceMock = null!;
    private Mock<ILogger<Api.Services.ContactService>> _loggerMock = null!;
    private Api.Services.ContactService _contactService = null!;

    public PaginationTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        await _factory.CleanDatabaseAsync(); // Added to ensure database is empty before seeding in each test
        _context = _factory.CreateDbContext();
        _cacheMock = new Mock<IDistributedCache>();
        _uploadServiceMock = new Mock<IUploadServiceClient>();
        _countryServiceMock = new Mock<ICountryServiceClient>();
        _loggerMock = new Mock<ILogger<Api.Services.ContactService>>();

        _countryServiceMock.Setup(x => x.ValidateCountryExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _contactService = new Api.Services.ContactService(_context, _cacheMock.Object, _uploadServiceMock.Object, _countryServiceMock.Object, _loggerMock.Object);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _factory.ResetDatabaseAsync();
    }

    [Fact]
    public async Task GetContactMessagesAsync_Should_Return_Correct_Page()
    {
        // Arrange
        var baseTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var contacts = new List<ContactMessage>();
        for (int i = 1; i <= 25; i++)
        {
            contacts.Add(new ContactMessage
            {
                FullName = $"User {i}",
                Email = $"user{i}@example.com",
                Subject = $"Subject {i}",
                Message = $"Message {i}",
                CountryId = Guid.Empty,
                ContactType = i % 2 == 0 ? ContactType.General : ContactType.Business,
                Priority = Priority.Medium,
                Status = ContactStatus.New,
                CreatedAt = baseTime.AddMinutes(-i), // More recent timestamps for lower numbers
                UpdatedAt = baseTime.AddMinutes(-i)   // More recent timestamps for lower numbers
            });
        }

        _context.ContactMessages.AddRange(contacts);
        await _context.SaveChangesAsync();

        // Act
        var result = await _contactService.GetContactMessagesAsync(page: 2, pageSize: 10);

        // Assert
        var resultList = result.ToList();
        Assert.Equal(10, resultList.Count);
        Assert.Equal("User 11", resultList.First().FullName); // The eleventh most recent user
        Assert.Equal("User 20", resultList.Last().FullName); // The twentieth most recent user
    }

    [Fact]
    public async Task GetContactMessagesAsync_Should_Return_Last_Page()
    {
        // Arrange
        var baseTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var contacts = new List<ContactMessage>();
        for (int i = 1; i <= 25; i++)
        {
            contacts.Add(new ContactMessage
            {
                FullName = $"User {i}",
                Email = $"user{i}@example.com",
                Subject = $"Subject {i}",
                Message = $"Message {i}",
                CountryId = Guid.Empty,
                ContactType = i % 2 == 0 ? ContactType.General : ContactType.Business,
                Priority = Priority.Medium,
                Status = ContactStatus.New,
                CreatedAt = baseTime.AddMinutes(-i), // More recent timestamps for lower numbers
                UpdatedAt = baseTime.AddMinutes(-i)   // More recent timestamps for lower numbers
            });
        }

        _context.ContactMessages.AddRange(contacts);
        await _context.SaveChangesAsync();

        // Act
        var result = await _contactService.GetContactMessagesAsync(page: 3, pageSize: 10);

        // Assert
        var resultList = result.ToList();
        Assert.Equal(5, resultList.Count); // Only 5 items on the last page (25 total, 10 per page × 2 full pages = 20, so 5 remaining)
        Assert.Equal("User 21", resultList.First().FullName); // The twenty-first most recent user
        Assert.Equal("User 25", resultList.Last().FullName); // The twenty-fifth (last) most recent user
    }

    [Fact]
    public async Task GetContactMessagesAsync_Should_Handle_Out_Of_Bounds_Page()
    {
        // Arrange
        var baseTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var contacts = new List<ContactMessage>();
        for (int i = 1; i <= 25; i++)
        {
            contacts.Add(new ContactMessage
            {
                FullName = $"User {i}",
                Email = $"user{i}@example.com",
                Subject = $"Subject {i}",
                Message = $"Message {i}",
                CountryId = Guid.Empty,
                ContactType = i % 2 == 0 ? ContactType.General : ContactType.Business,
                Priority = Priority.Medium,
                Status = ContactStatus.New,
                CreatedAt = baseTime.AddMinutes(-i), // More recent timestamps for lower numbers
                UpdatedAt = baseTime.AddMinutes(-i)   // More recent timestamps for lower numbers
            });
        }

        _context.ContactMessages.AddRange(contacts);
        await _context.SaveChangesAsync();

        // Act
        var result = await _contactService.GetContactMessagesAsync(page: 10, pageSize: 10);

        // Assert
        var resultList = result.ToList();
        Assert.Empty(resultList); // No items on page 10 when there are only 3 pages (25 items, 10 per page)
    }

    [Fact]
    public async Task GetContactMessagesAsync_Should_Work_With_Different_Page_Sizes()
    {
        // Arrange
        var baseTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var contacts = new List<ContactMessage>();
        for (int i = 1; i <= 25; i++)
        {
            contacts.Add(new ContactMessage
            {
                FullName = $"User {i}",
                Email = $"user{i}@example.com",
                Subject = $"Subject {i}",
                Message = $"Message {i}",
                CountryId = Guid.Empty,
                ContactType = i % 2 == 0 ? ContactType.General : ContactType.Business,
                Priority = Priority.Medium,
                Status = ContactStatus.New,
                CreatedAt = baseTime.AddMinutes(-i), // More recent timestamps for lower numbers
                UpdatedAt = baseTime.AddMinutes(-i)   // More recent timestamps for lower numbers
            });
        }

        _context.ContactMessages.AddRange(contacts);
        await _context.SaveChangesAsync();

        // Act
        var result = await _contactService.GetContactMessagesAsync(page: 1, pageSize: 5);

        // Assert
        var resultList = result.ToList();
        Assert.Equal(5, resultList.Count); // Only 5 items on the first page with pageSize=5
        Assert.Equal("User 1", resultList.First().FullName); // The most recent user
        Assert.Equal("User 5", resultList.Last().FullName); // The fifth most recent user
    }
}
