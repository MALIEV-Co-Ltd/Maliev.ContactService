using FluentAssertions;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Respawn;
using System.Data.Common;
using Testcontainers.PostgreSql;

namespace Maliev.ContactService.Tests.Services;

public class ContactServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer;
    private ContactDbContext? _context;
    private readonly IMemoryCache _cache;
    private readonly Mock<IUploadServiceClient> _uploadServiceMock;
    private readonly Mock<ILogger<Api.Services.ContactService>> _loggerMock;
    private Api.Services.ContactService? _contactService;
    private DbConnection? _connection;
    private Respawner? _respawner;

    public ContactServiceIntegrationTests()
    {
        _dbContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15-alpine")
            .Build();

        _cache = new MemoryCache(new MemoryCacheOptions());
        _uploadServiceMock = new Mock<IUploadServiceClient>();
        _loggerMock = new Mock<ILogger<Api.Services.ContactService>>();
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        var options = new DbContextOptionsBuilder<ContactDbContext>()
            .UseNpgsql(_dbContainer.GetConnectionString())
            .Options;

        _context = new ContactDbContext(options);
        await _context.Database.MigrateAsync();

        _contactService = new Api.Services.ContactService(_context, _cache, _uploadServiceMock.Object, _loggerMock.Object);

        // Set up Respawn for resetting database between tests
        _connection = _context.Database.GetDbConnection();
        await _connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres
        });
    }

    public async Task DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
        }
        if (_context != null)
        {
            await _context.DisposeAsync();
        }
        await _dbContainer.DisposeAsync();
        _cache.Dispose();
    }

    [Fact]
    public async Task CreateContactMessageAsync_Should_Create_Contact_Successfully()
    {
        // Guard clause to satisfy compiler
        if (_contactService == null || _connection == null || _respawner == null)
        {
            throw new InvalidOperationException("Test not properly initialized");
        }

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

        // Clean up for next test
        await _respawner.ResetAsync(_connection);
    }

    [Fact]
    public async Task CreateContactMessageAsync_With_Files_Should_Upload_Files()
    {
        // Guard clause to satisfy compiler
        if (_contactService == null || _connection == null || _respawner == null)
        {
            throw new InvalidOperationException("Test not properly initialized");
        }

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

        // Clean up for next test
        await _respawner.ResetAsync(_connection);
    }

    [Fact]
    public async Task GetContactMessageByIdAsync_Should_Return_Contact_When_Exists()
    {
        // Guard clause to satisfy compiler
        if (_contactService == null || _context == null || _connection == null || _respawner == null)
        {
            throw new InvalidOperationException("Test not properly initialized");
        }

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

        // Clean up for next test
        await _respawner.ResetAsync(_connection);
    }

    [Fact]
    public async Task GetContactMessagesAsync_Should_Return_Paginated_Results()
    {
        // Guard clause to satisfy compiler
        if (_contactService == null || _context == null || _connection == null || _respawner == null)
        {
            throw new InvalidOperationException("Test not properly initialized");
        }

        // Arrange
        var baseTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc); // Fixed time for predictable results
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
        var result = await _contactService.GetContactMessagesAsync(page: 1, pageSize: 10);

        // Assert
        var resultList = result.ToList();
        resultList.Should().HaveCount(10);
        
        // The ordering should be by CreatedAt descending (most recent first)
        // User 1 has the most recent CreatedAt timestamp (baseTime.AddMinutes(-1)), so it should be first
        resultList.First().FullName.Should().Be("User 1");

        // Clean up for next test
        await _respawner.ResetAsync(_connection);
    }

    [Fact]
    public async Task UpdateContactStatusAsync_Should_Update_Status_Successfully()
    {
        // Guard clause to satisfy compiler
        if (_contactService == null || _context == null || _connection == null || _respawner == null)
        {
            throw new InvalidOperationException("Test not properly initialized");
        }

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

        // Clean up for next test
        await _respawner.ResetAsync(_connection);
    }

    [Fact]
    public async Task DeleteContactMessageAsync_Should_Delete_Successfully()
    {
        // Guard clause to satisfy compiler
        if (_contactService == null || _context == null || _connection == null || _respawner == null)
        {
            throw new InvalidOperationException("Test not properly initialized");
        }

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
        await _contactService.DeleteContactMessageAsync(contact.Id);

        // Assert
        var deletedContact = await _context.ContactMessages.FindAsync(contact.Id);
        deletedContact.Should().BeNull();

        // Clean up for next test
        await _respawner.ResetAsync(_connection);
    }
}
