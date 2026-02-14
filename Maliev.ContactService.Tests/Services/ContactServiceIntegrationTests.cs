using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
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
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<IUploadServiceClient> _uploadServiceMock;
    private readonly Mock<ICountryServiceClient> _countryServiceMock;
    private readonly Mock<ILogger<Api.Services.ContactService>> _loggerMock;
    private Api.Services.ContactService? _contactService;
    private DbConnection? _connection;
    private Respawner? _respawner;

    public ContactServiceIntegrationTests()
    {
        _dbContainer = new PostgreSqlBuilder().WithName("postgres:18-alpine")
            .Build();

        _cacheMock = new Mock<IDistributedCache>();
        _uploadServiceMock = new Mock<IUploadServiceClient>();
        _countryServiceMock = new Mock<ICountryServiceClient>();
        _loggerMock = new Mock<ILogger<Api.Services.ContactService>>();

        // Setup default behavior for country service mock
        _countryServiceMock.Setup(x => x.ValidateCountryExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        var options = new DbContextOptionsBuilder<ContactDbContext>()
            .UseNpgsql(_dbContainer.GetConnectionString())
            .Options;

        _context = new ContactDbContext(options);
        await _context.Database.MigrateAsync();

        _contactService = new Api.Services.ContactService(_context, _cacheMock.Object, _uploadServiceMock.Object, _countryServiceMock.Object, _loggerMock.Object);

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
            CountryId = Guid.Empty,
            ContactType = ContactType.General,
            Priority = Priority.High
        };

        // Act
        var result = await _contactService.CreateContactMessageAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("John Doe", result.FullName);
        Assert.Equal("john.doe@example.com", result.Email);
        Assert.Equal("Test Inquiry", result.Subject);
        Assert.Equal("This is a test message", result.Message);
        Assert.Equal(ContactType.General, result.ContactType);
        Assert.Equal(Priority.High, result.Priority);
        Assert.Equal(ContactStatus.New, result.Status);
        Assert.True((DateTime.UtcNow - result.CreatedAt).Duration() < TimeSpan.FromSeconds(5));
        Assert.True((DateTime.UtcNow - result.UpdatedAt).Duration() < TimeSpan.FromSeconds(5));

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
        var result = await _contactService.GetContactMessagesAsync(page: 1, pageSize: 10);

        // Assert
        var resultList = result.ToList();
        Assert.Equal(10, resultList.Count);

        // The ordering should be by CreatedAt descending (most recent first)
        // User 1 has the most recent CreatedAt timestamp (baseTime.AddMinutes(-1)), so it should be first
        Assert.Equal("User 1", resultList.First().FullName);

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

        // Clean up for next test
        await _respawner.ResetAsync(_connection);
    }
}
