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

public class ContactServiceTests : IDisposable
{
    private readonly ContactDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly Mock<IUploadServiceClient> _uploadServiceMock;
    private readonly Mock<ILogger<Api.Services.ContactService>> _loggerMock;
    private readonly Api.Services.ContactService _contactService;

    public ContactServiceTests()
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
    public async Task CreateContactMessageAsync_Should_Create_Contact_Successfully()
    {
        // Arrange
        var request = new CreateContactMessageRequest
        {
            FullName = "John Doe",
            Email = "john.doe@example.com",
            Subject = "Test Inquiry",
            Message = "This is a test message",
            ContactType = ContactType.General,
            Priority = Priority.High
        };

        // Act
        var result = await _contactService.CreateContactMessageAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.FullName.Should().Be("John Doe");
        result.Email.Should().Be("john.doe@example.com");
        result.Subject.Should().Be("Test Inquiry");
        result.Message.Should().Be("This is a test message");
        result.ContactType.Should().Be(ContactType.General);
        result.Priority.Should().Be(Priority.High);
        result.Status.Should().Be(ContactStatus.New);
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
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
            ContactType = ContactType.Quotation,
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
        result.Should().NotBeNull();
        result.Files.Should().HaveCount(1);
        result.Files.First().FileName.Should().Be("requirements.pdf");
        result.Files.First().ContentType.Should().Be("application/pdf");
        result.Files.First().UploadServiceFileId.Should().Be("upload-123");
        result.Files.First().FileSize.Should().Be(5);

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
        result.Should().NotBeNull();
        result!.Id.Should().Be(contact.Id);
        result.FullName.Should().Be("Test User");
        result.Email.Should().Be("test@example.com");
        result.ContactType.Should().Be(ContactType.Business);
    }

    [Fact]
    public async Task GetContactMessageByIdAsync_Should_Return_Null_When_Not_Exists()
    {
        // Act
        var result = await _contactService.GetContactMessageByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetContactMessageByIdAsync_Should_Use_Cache()
    {
        // Arrange
        var contact = new ContactMessage
        {
            FullName = "Cached User",
            Email = "cached@example.com",
            Subject = "Cached Subject",
            Message = "Cached Message",
            ContactType = ContactType.General,
            Priority = Priority.Low,
            Status = ContactStatus.New,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ContactMessages.Add(contact);
        await _context.SaveChangesAsync();

        // Act - First call should hit database
        var result1 = await _contactService.GetContactMessageByIdAsync(contact.Id);

        // Act - Second call should hit cache
        var result2 = await _contactService.GetContactMessageByIdAsync(contact.Id);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1!.Id.Should().Be(result2!.Id);
        result1.FullName.Should().Be(result2.FullName);
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
        result.Should().NotBeNull();
        result!.Status.Should().Be(ContactStatus.InProgress);
        result.Priority.Should().Be(Priority.High);
        result.UpdatedAt.Should().BeAfter(contact.CreatedAt);
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
        result.Should().NotBeNull();
        result!.Status.Should().Be(ContactStatus.Resolved);
        result.ResolvedAt.Should().NotBeNull();
        result.ResolvedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetContactMessagesAsync_Should_Return_Paginated_Results()
    {
        // Arrange
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
                CreatedAt = DateTime.UtcNow.AddMinutes(-i),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }

        _context.ContactMessages.AddRange(contacts);
        await _context.SaveChangesAsync();

        // Act
        var result = await _contactService.GetContactMessagesAsync(page: 1, pageSize: 10);

        // Assert
        result.Should().HaveCount(10);
        result.First().FullName.Should().Be("User 1"); // Most recent first
    }

    [Fact]
    public async Task GetContactMessagesAsync_Should_Filter_By_Status()
    {
        // Arrange
        _context.ContactMessages.AddRange(new[]
        {
            new ContactMessage { FullName = "User 1", Email = "user1@example.com", Subject = "Subject 1", Message = "Message 1", Status = ContactStatus.New, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ContactMessage { FullName = "User 2", Email = "user2@example.com", Subject = "Subject 2", Message = "Message 2", Status = ContactStatus.InProgress, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ContactMessage { FullName = "User 3", Email = "user3@example.com", Subject = "Subject 3", Message = "Message 3", Status = ContactStatus.Resolved, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _contactService.GetContactMessagesAsync(status: ContactStatus.InProgress);

        // Assert
        result.Should().HaveCount(1);
        result.First().FullName.Should().Be("User 2");
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
            ContactType = ContactType.General,
            Priority = Priority.Medium,
            Status = ContactStatus.New,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ContactMessages.Add(contact);
        await _context.SaveChangesAsync();

        // Act
        var result = await _contactService.DeleteContactMessageAsync(contact.Id);

        // Assert
        result.Should().BeTrue();

        var deletedContact = await _context.ContactMessages.FindAsync(contact.Id);
        deletedContact.Should().BeNull();
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
            UploadServiceFileId = "upload-456",
            CreatedAt = DateTime.UtcNow
        };

        _context.ContactFiles.Add(contactFile);
        await _context.SaveChangesAsync();

        _uploadServiceMock.Setup(x => x.DeleteFileAsync("upload-456"))
            .ReturnsAsync(true);

        // Act
        var result = await _contactService.DeleteContactFileAsync(contact.Id, contactFile.Id);

        // Assert
        result.Should().BeTrue();
        _uploadServiceMock.Verify(x => x.DeleteFileAsync("upload-456"), Times.Once);

        var deletedFile = await _context.ContactFiles.FindAsync(contactFile.Id);
        deletedFile.Should().BeNull();
    }

    public void Dispose()
    {
        _context?.Dispose();
        _cache?.Dispose();
    }
}