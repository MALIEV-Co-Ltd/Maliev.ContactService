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

public class ContactServiceTests : IClassFixture<CustomWebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private ContactDbContext _context = null!;
    private Mock<IDistributedCache> _cacheMock = null!;
    private Mock<IUploadServiceClient> _uploadServiceMock = null!;
    private Mock<ICountryServiceClient> _countryServiceMock = null!;
    private Mock<ILogger<Api.Services.ContactService>> _loggerMock = null!;
    private Api.Services.ContactService _contactService = null!;
    private const string ListCacheVersionKey = "contact-list-version";

    public ContactServiceTests(CustomWebApplicationFactory<Program> factory)
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

        _countryServiceMock.Setup(x => x.ValidateCountryExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _cacheMock.Setup(c => c.GetAsync(ListCacheVersionKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        _contactService = new Api.Services.ContactService(_context, _cacheMock.Object, _uploadServiceMock.Object, _countryServiceMock.Object, _loggerMock.Object);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _factory.ResetDatabaseAsync();
    }

    [Fact]
    public async Task CreateContactMessageAsync_Should_Create_Contact_Successfully()
    {
        // Arrange
        var request = new CreateContactMessageRequest
        {
            FullName = "John Doe",
            Email = "john.doe@example.com",
            Subject = "Test Inquiry",
            Message = "This is a test message",
            CountryId = Guid.Empty,
            ContactType = ContactType.General,
            Priority = Priority.High,
            Files = new List<CreateContactFileRequest>()
        };

        // Act
        var result = await _contactService.CreateContactMessageAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("John Doe", result.FullName);
    }

    [Fact]
    public async Task CreateContactMessageAsync_With_Files_Should_Upload_Files()
    {
        // Arrange
        var request = new CreateContactMessageRequest
        {
            FullName = "Jane Doe",
            Email = "jane.doe@example.com",
            Subject = "Quotation Request",
            Message = "Please provide a quote",
            CountryId = Guid.Empty,
            ContactType = ContactType.General,
            Files = new List<CreateContactFileRequest>
            {
                new CreateContactFileRequest
                {
                    FileName = "requirements.pdf",
                    FileContent = new byte[] { 1, 2, 3, 4, 5 },
                    ContentType = "application/pdf"
                }
            }
        };

        _uploadServiceMock.Setup(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new UploadResponse
            {
                FileId = "upload-123",
                ObjectName = "contacts/1/requirements.pdf",
                Bucket = "test-bucket",
                FileSize = 5,
                UploadedAt = DateTime.UtcNow
            });

        // Act
        var result = await _contactService.CreateContactMessageAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Files);
        Assert.Equal("requirements.pdf", result.Files.First().FileName);
        Assert.Equal("application/pdf", result.Files.First().ContentType);
        Assert.Equal("upload-123", result.Files.First().UploadServiceFileId);
        Assert.Equal(5, result.Files.First().FileSize);

        _uploadServiceMock.Verify(x => x.UploadFileAsync(
            It.Is<string>(s => s.Contains("contacts/") && s.Contains("requirements.pdf")),
            It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 1, 2, 3, 4, 5 })),
            "application/pdf",
            "requirements.pdf"), Times.Once);
    }

    [Fact]
    public async Task GetContactMessageByIdAsync_Should_Return_Contact_When_Exists()
    {
        // Arrange
        var contact = new ContactMessage
        {
            FullName = "Test User",
            Email = "test@example.com",
            Subject = "Test Subject",
            Message = "Test Message",
            CountryId = Guid.Empty,
            ContactType = ContactType.Business,
            Priority = Priority.Medium,
            Status = ContactStatus.New,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ContactMessages.Add(contact);
        await _context.SaveChangesAsync();

        // Act
        var result = await _contactService.GetContactMessageByIdAsync(contact.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(contact.Id, result!.Id);
        Assert.Equal("Test User", result.FullName);
        Assert.Equal("test@example.com", result.Email);
        Assert.Equal(ContactType.Business, result.ContactType);
    }

    [Fact]
    public async Task GetContactMessageByIdAsync_Should_Return_Null_When_Not_Exists()
    {
        // Act
        var result = await _contactService.GetContactMessageByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetContactMessageByIdAsync_Should_Use_Cache()
    {
        // Arrange
        var contactId = 1;
        var cacheKey = $"contact_message_{contactId}";
        var dto = new ContactMessageDto { Id = contactId, FullName = "Cached User", Email = "cached@example.com", Subject = "Test", Message = "Test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var serializedDto = JsonSerializer.Serialize(dto);
        var responseBytes = Encoding.UTF8.GetBytes(serializedDto);
        _cacheMock.Setup(c => c.GetAsync(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(responseBytes);

        // Act
        var result = await _contactService.GetContactMessageByIdAsync(contactId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(contactId, result!.Id);
        _cacheMock.Verify(c => c.GetAsync(cacheKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateContactStatusAsync_Should_Update_Status_Successfully()
    {
        // Arrange
        var contact = new ContactMessage
        {
            FullName = "Update Test",
            Email = "update@example.com",
            Subject = "Update Subject",
            Message = "Update Message",
            CountryId = Guid.Empty,
            ContactType = ContactType.General,
            Priority = Priority.Medium,
            Status = ContactStatus.New,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ContactMessages.Add(contact);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateContactStatusRequest
        {
            Status = ContactStatus.InProgress,
            Priority = Priority.High
        };

        // Act
        var result = await _contactService.UpdateContactStatusAsync(contact.Id, updateRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ContactStatus.InProgress, result!.Status);
        Assert.Equal(Priority.High, result.Priority);
        Assert.True(result.UpdatedAt > contact.CreatedAt);
    }

    [Fact]
    public async Task UpdateContactStatusAsync_Should_Log_Warning_When_Potentially_Concurrent()
    {
        // Arrange
        var contact = new ContactMessage
        {
            FullName = "Concurrent Test",
            Email = "concurrent@example.com",
            Subject = "Subject",
            Message = "Message",
            CountryId = Guid.Empty,
            ContactType = ContactType.General,
            Priority = Priority.Medium,
            Status = ContactStatus.New,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-2) // Within 5 minutes
        };

        _context.ContactMessages.Add(contact);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateContactStatusRequest
        {
            Status = ContactStatus.InProgress
        };

        // Act
        await _contactService.UpdateContactStatusAsync(contact.Id, updateRequest);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Potential concurrent update")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateContactStatusAsync_Should_Set_ResolvedAt_When_Resolved()

    {
        // Arrange
        var contact = new ContactMessage
        {
            FullName = "Resolve Test",
            Email = "resolve@example.com",
            Subject = "Resolve Subject",
            Message = "Resolve Message",
            CountryId = Guid.Empty,
            ContactType = ContactType.General,
            Priority = Priority.Medium,
            Status = ContactStatus.InProgress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ContactMessages.Add(contact);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateContactStatusRequest
        {
            Status = ContactStatus.Resolved
        };

        // Act
        var result = await _contactService.UpdateContactStatusAsync(contact.Id, updateRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ContactStatus.Resolved, result!.Status);
        Assert.NotNull(result.ResolvedAt);
        Assert.True((DateTime.UtcNow - result.ResolvedAt.Value).Duration() < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetContactMessagesAsync_Should_Return_Paginated_Results_In_Correct_Order()
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
                CreatedAt = baseTime.AddMinutes(-i),
                UpdatedAt = baseTime.AddMinutes(-i)
            });
        }

        _context.ContactMessages.AddRange(contacts);
        await _context.SaveChangesAsync();

        // Act
        var result = await _contactService.GetContactMessagesAsync(page: 1, pageSize: 10);

        // Assert
        var resultList = result.ToList();
        Assert.Equal(10, resultList.Count);
        Assert.Equal("User 1", resultList.First().FullName);
        Assert.Equal("User 10", resultList.Last().FullName);
    }

    [Fact]
    public async Task GetContactMessagesAsync_Should_Filter_By_Status()
    {
        // Arrange
        _context.ContactMessages.AddRange(new[]
        {
            new ContactMessage { FullName = "User 1", Email = "user1@example.com", Subject = "Subject 1", Message = "Message 1", CountryId = Guid.Empty, Status = ContactStatus.New, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ContactMessage { FullName = "User 2", Email = "user2@example.com", Subject = "Subject 2", Message = "Message 2", CountryId = Guid.Empty, Status = ContactStatus.InProgress, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ContactMessage { FullName = "User 3", Email = "user3@example.com", Subject = "Subject 3", Message = "Message 3", CountryId = Guid.Empty, Status = ContactStatus.Resolved, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _contactService.GetContactMessagesAsync(status: ContactStatus.InProgress);

        // Assert
        Assert.Single(result);
        Assert.Equal("User 2", result.First().FullName);
    }

    [Fact]
    public async Task GetContactMessagesAsync_Should_Filter_By_ContactType()
    {
        // Arrange
        _context.ContactMessages.AddRange(new[]
        {
            new ContactMessage { FullName = "User 1", Email = "user1@example.com", Subject = "Subject 1", Message = "Message 1", CountryId = Guid.Empty, ContactType = ContactType.General, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ContactMessage { FullName = "User 2", Email = "user2@example.com", Subject = "Subject 2", Message = "Message 2", CountryId = Guid.Empty, ContactType = ContactType.Business, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _contactService.GetContactMessagesAsync(contactType: ContactType.Business);

        // Assert
        Assert.Single(result);
        Assert.Equal("User 2", result.First().FullName);
    }

    [Fact]
    public async Task GetContactMessagesAsync_Should_Filter_By_Email()
    {
        // Arrange
        _context.ContactMessages.AddRange(new[]
        {
            new ContactMessage { FullName = "User 1", Email = "user1@example.com", Subject = "Subject 1", Message = "Message 1", CountryId = Guid.Empty, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ContactMessage { FullName = "User 2", Email = "user2@example.com", Subject = "Subject 2", Message = "Message 2", CountryId = Guid.Empty, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _contactService.GetContactMessagesAsync(email: "USER1@EXAMPLE.COM");

        // Assert
        Assert.Single(result);
        Assert.Equal("User 1", result.First().FullName);
    }

    [Fact]
    public async Task GetContactMessagesAsync_Should_Return_Empty_On_Cache_Miss_And_Empty_Db()
    {
        // Act
        var result = await _contactService.GetContactMessagesAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetContactMessagesAsync_Should_Handle_Empty_Cached_Bytes()
    {
        // Arrange
        _cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<byte>());

        // Act
        var result = await _contactService.GetContactMessagesAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetContactMessagesAsync_Should_Return_Cached_Results()
    {
        // Arrange
        var dtos = new List<ContactMessageDto>
        {
            new ContactMessageDto { Id = 1, FullName = "Cached", Email = "c@e.com", Subject = "S", Message = "M", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        };
        var serialized = JsonSerializer.Serialize((IEnumerable<ContactMessageDto>)dtos);
        var bytes = Encoding.UTF8.GetBytes(serialized);

        _cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

        // Act
        var result = await _contactService.GetContactMessagesAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Cached", result.First().FullName);
    }

    [Fact]
    public async Task GetContactFileByIdAsync_Should_Return_File_When_Exists()
    {
        // Arrange
        var contact = new ContactMessage { FullName = "Test", Email = "test@example.com", Subject = "Test", Message = "Test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        _context.ContactMessages.Add(contact);
        await _context.SaveChangesAsync();

        var file = new ContactFile
        {
            ContactMessageId = contact.Id,
            FileName = "test.txt",
            ObjectName = "obj",
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.ContactFiles.Add(file);
        await _context.SaveChangesAsync();

        // Act
        var result = await _contactService.GetContactFileByIdAsync(contact.Id, file.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(file.Id, result!.Id);
    }

    [Fact]
    public async Task DeleteContactFileAsync_Should_Not_Call_UploadService_When_No_FileId()
    {
        // Arrange
        var contact = new ContactMessage { FullName = "Test", Email = "test@example.com", Subject = "Test", Message = "Test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        _context.ContactMessages.Add(contact);
        await _context.SaveChangesAsync();

        var file = new ContactFile
        {
            ContactMessageId = contact.Id,
            FileName = "test.txt",
            ObjectName = "obj",
            UploadServiceFileId = null, // No file ID
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.ContactFiles.Add(file);
        await _context.SaveChangesAsync();

        // Act
        await _contactService.DeleteContactFileAsync(contact.Id, file.Id);

        // Assert
        _uploadServiceMock.Verify(x => x.DeleteFileAsync(It.IsAny<string>()), Times.Never);
        var deletedFile = await _context.ContactFiles.FindAsync(file.Id);
        Assert.Null(deletedFile);
    }

    [Fact]
    public async Task CreateContactMessageAsync_Should_Throw_DuplicateInquiryException_When_Duplicate_Detected()
    {
        // Arrange
        var email = "duplicate@example.com";
        var contact = new ContactMessage
        {
            FullName = "First",
            Email = email,
            Subject = "Sub",
            Message = "Msg",
            CountryId = Guid.Empty,
            CreatedAt = DateTimeOffset.UtcNow.AddSeconds(-30)
        };
        _context.ContactMessages.Add(contact);
        await _context.SaveChangesAsync();

        var request = new CreateContactMessageRequest
        {
            FullName = "Second",
            Email = email,
            Subject = "Sub",
            Message = "Msg",
            CountryId = Guid.Empty
        };

        // Act & Assert
        await Assert.ThrowsAsync<DuplicateInquiryException>(() => _contactService.CreateContactMessageAsync(request));
    }

    [Fact]
    public async Task CreateContactMessageAsync_Should_Throw_ArgumentException_When_Country_Invalid()
    {
        // Arrange
        var countryId = Guid.NewGuid();
        var request = new CreateContactMessageRequest
        {
            FullName = "Test",
            Email = "test@example.com",
            Subject = "Sub",
            Message = "Msg",
            CountryId = countryId
        };
        _countryServiceMock.Setup(x => x.ValidateCountryExistsAsync(countryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _contactService.CreateContactMessageAsync(request));
        Assert.Contains($"Country ID {countryId} is not valid", ex.Message);
    }

    [Fact]
    public async Task GetContactFilesAsync_Should_Return_Files()
    {
        // Arrange
        var contact = new ContactMessage { FullName = "Test", Email = "test@example.com", Subject = "Test", Message = "Test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        _context.ContactMessages.Add(contact);
        await _context.SaveChangesAsync();

        var file = new ContactFile
        {
            ContactMessageId = contact.Id,
            FileName = "test.txt",
            ObjectName = "obj",
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.ContactFiles.Add(file);
        await _context.SaveChangesAsync();

        // Act
        var result = await _contactService.GetContactFilesAsync(contact.Id);

        // Assert
        Assert.Single(result);
        Assert.Equal(file.FileName, result.First().FileName);
    }

    [Fact]
    public async Task GetCacheVersionAsync_Should_Handle_Invalid_Bytes()
    {
        // Arrange
        _cacheMock.Setup(c => c.GetAsync(ListCacheVersionKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 }); // Less than 8 bytes

        // Act
        // This will be triggered by calling GetContactMessagesAsync
        var result = await _contactService.GetContactMessagesAsync();

        // Assert
        // We can't directly check the private version, but we ensure it doesn't crash and uses default 1
        Assert.Empty(result);
    }

    [Fact]
    public async Task UpdateContactStatusAsync_Should_Throw_InvalidOperationException_On_Concurrency_Conflict()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ContactDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new MockContactDbContext(options);
        var contact = new ContactMessage
        {
            Id = 1,
            FullName = "Test",
            Email = "test@example.com",
            Subject = "Sub",
            Message = "Msg",
            CountryId = Guid.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        context.ContactMessages.Add(contact);
        await context.SaveChangesAsync();

        var service = new Api.Services.ContactService(context, _cacheMock.Object, _uploadServiceMock.Object, _countryServiceMock.Object, _loggerMock.Object);
        var updateRequest = new UpdateContactStatusRequest { Status = ContactStatus.InProgress };

        context.ThrowOnSave = true;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateContactStatusAsync(contact.Id, updateRequest));
    }

    private class MockContactDbContext : ContactDbContext
    {
        public bool ThrowOnSave { get; set; }
        public MockContactDbContext(DbContextOptions<ContactDbContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnSave) throw new DbUpdateConcurrencyException();
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task DeleteContactMessageAsync_Should_Delete_Successfully()

    {
        // Arrange
        var contact = new ContactMessage
        {
            FullName = "Delete Test",
            Email = "delete@example.com",
            Subject = "Delete Subject",
            Message = "Delete Message",
            CountryId = Guid.Empty,
            ContactType = ContactType.General,
            Priority = Priority.Medium,
            Status = ContactStatus.New,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ContactMessages.Add(contact);
        await _context.SaveChangesAsync();

        // Act
        await _contactService.DeleteContactMessageAsync(contact.Id);

        // Assert
        var deletedContact = await _context.ContactMessages.FindAsync(contact.Id);
        Assert.Null(deletedContact);
    }

    [Fact]
    public async Task DeleteContactFileAsync_Should_Delete_From_UploadService_And_Database()
    {
        // Arrange
        var contact = new ContactMessage
        {
            FullName = "File Delete Test",
            Email = "filedelete@example.com",
            Subject = "File Delete Subject",
            Message = "File Delete Message",
            CountryId = Guid.Empty,
            ContactType = ContactType.General,
            Priority = Priority.Medium,
            Status = ContactStatus.New,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ContactMessages.Add(contact);
        await _context.SaveChangesAsync();

        var contactFile = new ContactFile
        {
            ContactMessageId = contact.Id,
            FileName = "test.pdf",
            ObjectName = $"contacts/{contact.Id}/test.pdf",
            FileSize = 1024,
            ContentType = "application/pdf",
            UploadServiceFileId = "file123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.ContactFiles.Add(contactFile);
        await _context.SaveChangesAsync();

        // Act
        await _contactService.DeleteContactFileAsync(contact.Id, contactFile.Id);

        // Assert
        var deletedFile = await _context.ContactFiles.FindAsync(contactFile.Id);
        Assert.Null(deletedFile);
    }

    [Fact]
    public async Task DeleteContactFileAsync_Should_Continue_When_UploadService_Fails()
    {
        // Arrange
        var contact = new ContactMessage
        {
            FullName = "File Delete Fail Test",
            Email = "filedeletefail@example.com",
            Subject = "Subject",
            Message = "Message",
            CountryId = Guid.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.ContactMessages.Add(contact);
        await _context.SaveChangesAsync();

        var contactFile = new ContactFile
        {
            ContactMessageId = contact.Id,
            FileName = "test.pdf",
            ObjectName = "obj",
            UploadServiceFileId = "file123",
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.ContactFiles.Add(contactFile);
        await _context.SaveChangesAsync();

        _uploadServiceMock.Setup(x => x.DeleteFileAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Delete failed"));

        // Act
        await _contactService.DeleteContactFileAsync(contact.Id, contactFile.Id);

        // Assert
        var deletedFile = await _context.ContactFiles.FindAsync(contactFile.Id);
        Assert.Null(deletedFile);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to delete file from UploadService")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateContactMessageAsync_Should_Throw_ArgumentNullException_When_Request_Is_Null()

    {
        Func<Task> act = async () => await _contactService.CreateContactMessageAsync(null!);
        await Assert.ThrowsAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task CreateContactMessageAsync_Should_Continue_When_Upload_Fails()
    {
        // Arrange
        var request = new CreateContactMessageRequest
        {
            FullName = "Upload Fail Test",
            Email = "uploadfail@example.com",
            Subject = "Upload Fail Test Subject",
            Message = "This should still be created even though file upload fails",
            ContactType = ContactType.General,
            CountryId = Guid.Empty,
            Files = new List<CreateContactFileRequest>
            {
                new CreateContactFileRequest
                {
                    FileName = "test.pdf",
                    FileContent = new byte[] { 1, 2, 3, 4, 5 },
                    ContentType = "application/pdf"
                }
            }
        };

        _uploadServiceMock.Setup(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Upload failed"));

        // Act
        var result = await _contactService.CreateContactMessageAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("Upload Fail Test", result.FullName);
        Assert.Empty(result.Files);

        _loggerMock.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Error),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() != null && v.ToString()!.Contains("Failed to upload file")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateContactStatusAsync_Should_Throw_NotFoundException_When_Contact_Not_Exists()
    {
        var updateRequest = new UpdateContactStatusRequest { Status = ContactStatus.InProgress };
        Func<Task> act = async () => await _contactService.UpdateContactStatusAsync(999, updateRequest);
        await Assert.ThrowsAsync<NotFoundException>(act);
    }

    [Fact]
    public async Task DeleteContactMessageAsync_Should_Throw_NotFoundException_When_Contact_Not_Exists()
    {
        Func<Task> act = async () => await _contactService.DeleteContactMessageAsync(999);
        await Assert.ThrowsAsync<NotFoundException>(act);
    }

    [Fact]
    public async Task DeleteContactFileAsync_Should_Throw_NotFoundException_When_File_Not_Exists()
    {
        // Arrange
        var contact = new ContactMessage { FullName = "Test User", Email = "test@example.com", Subject = "Test", Message = "Test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        _context.ContactMessages.Add(contact);
        await _context.SaveChangesAsync();
        Func<Task> act = async () => await _contactService.DeleteContactFileAsync(contact.Id, 999);
        await Assert.ThrowsAsync<NotFoundException>(act);
    }
}
