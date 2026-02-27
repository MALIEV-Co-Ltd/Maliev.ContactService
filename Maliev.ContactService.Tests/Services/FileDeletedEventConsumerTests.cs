using Maliev.ContactService.Api.Consumers;
using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Data.Models;
using Maliev.MessagingContracts.Contracts.Uploads;
using Maliev.MessagingContracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.PostgreSql;
using Xunit;

namespace Maliev.ContactService.Tests.Services;

public class FileDeletedEventConsumerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly Mock<ILogger<FileDeletedEventConsumer>> _loggerMock;

    public FileDeletedEventConsumerTests()
    {
        _postgresContainer = new PostgreSqlBuilder().WithImage("postgres:18-alpine")
            .Build();
        _loggerMock = new Mock<ILogger<FileDeletedEventConsumer>>();
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        var options = new DbContextOptionsBuilder<ContactDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        using var dbContext = new ContactDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
    }

    private ContactDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ContactDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;
        return new ContactDbContext(options);
    }

    [Fact]
    public async Task Consume_WhenServiceIdIsNotContactService_ShouldIgnore()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var consumer = new FileDeletedEventConsumer(dbContext, _loggerMock.Object);
        var message = new FileDeletedEvent
        {
            Payload = new FileDeletedEventPayload
            {
                ServiceId = "other-service",
                FileId = "file-1",
                StoragePath = "path/1"
            }
        };
        var contextMock = new Mock<ConsumeContext<FileDeletedEvent>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Ignoring FileDeletedEvent")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WhenValidMessage_ShouldRemoveFileReferences()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var messageEntity = new ContactMessage
        {
            FullName = "Test User",
            Email = "test@example.com",
            Subject = "Test Subject",
            Message = "Test Message",
            CountryId = Guid.Empty
        };
        dbContext.ContactMessages.Add(messageEntity);
        await dbContext.SaveChangesAsync();

        var contactFile = new ContactFile
        {
            ContactMessageId = messageEntity.Id,
            FileName = "test.txt",
            ObjectName = "path/1",
            UploadServiceFileId = "file-1"
        };
        dbContext.ContactFiles.Add(contactFile);
        await dbContext.SaveChangesAsync();

        var consumer = new FileDeletedEventConsumer(dbContext, _loggerMock.Object);
        var message = new FileDeletedEvent
        {
            Payload = new FileDeletedEventPayload
            {
                ServiceId = "contact-service",
                FileId = "file-1",
                StoragePath = "path/1"
            }
        };
        var contextMock = new Mock<ConsumeContext<FileDeletedEvent>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        using var assertContext = CreateDbContext();
        var filesInDb = await assertContext.ContactFiles.ToListAsync();
        Assert.Empty(filesInDb);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Removed 1 contact file references")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
