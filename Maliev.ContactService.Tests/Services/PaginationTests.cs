using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.ContactService.Tests.Services;

public class PaginationTests : IDisposable
{
    private readonly ContactDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly Mock<IUploadServiceClient> _uploadServiceMock;
    private readonly Mock<ILogger<Api.Services.ContactService>> _loggerMock;
    private readonly Api.Services.ContactService _contactService;

    public PaginationTests()
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
        resultList.Should().HaveCount(10);
        resultList.First().FullName.Should().Be("User 11"); // The eleventh most recent user
        resultList.Last().FullName.Should().Be("User 20"); // The twentieth most recent user
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
        resultList.Should().HaveCount(5); // Only 5 items on the last page (25 total, 10 per page × 2 full pages = 20, so 5 remaining)
        resultList.First().FullName.Should().Be("User 21"); // The twenty-first most recent user
        resultList.Last().FullName.Should().Be("User 25"); // The twenty-fifth (last) most recent user
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
        resultList.Should().BeEmpty(); // No items on page 10 when there are only 3 pages (25 items, 10 per page)
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
        resultList.Should().HaveCount(5); // Only 5 items on the first page with pageSize=5
        resultList.First().FullName.Should().Be("User 1"); // The most recent user
        resultList.Last().FullName.Should().Be("User 5"); // The fifth most recent user
    }

    public void Dispose()
    {
        _context?.Dispose();
        _cache?.Dispose();
    }
}