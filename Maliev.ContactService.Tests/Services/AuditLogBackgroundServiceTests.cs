using System.Threading.Channels;
using Maliev.ContactService.Api.Services.Auth;
using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.PostgreSql;
using Xunit;

namespace Maliev.ContactService.Tests.Services;

public class AuditLogBackgroundServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly Mock<ILogger<AuditLogBackgroundService>> _loggerMock;

    public AuditLogBackgroundServiceTests()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:18-alpine")
            .Build();
        _loggerMock = new Mock<ILogger<AuditLogBackgroundService>>();
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

    private ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ContactDbContext>(options =>
            options.UseNpgsql(_postgresContainer.GetConnectionString()));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ExecuteAsync_ProcessesLogEntry()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<AuditLog>();
        var serviceProvider = CreateServiceProvider();
        var service = new AuditLogBackgroundService(channel, serviceProvider, _loggerMock.Object);

        var logEntry = new AuditLog
        {
            UserId = "test-user",
            Action = "test-action",
            Resource = "test-resource",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var cts = new CancellationTokenSource();
        var executeTask = service.StartAsync(cts.Token);

        await channel.Writer.WriteAsync(logEntry);

        // Wait a bit for processing
        await Task.Delay(500);

        cts.Cancel();
        await executeTask;

        // Assert
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ContactDbContext>();
        var logInDb = await dbContext.AuditLogs.FirstOrDefaultAsync(l => l.UserId == "test-user");
        Assert.NotNull(logInDb);
        Assert.Equal("test-action", logInDb.Action);
    }

    [Fact]
    public async Task ExecuteAsync_RetriesOnFailure()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<AuditLog>();

        // Mock service provider to return a failing DbContext
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockScope = new Mock<IServiceScope>();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();

        mockServiceProvider.Setup(s => s.GetService(typeof(IServiceScopeFactory))).Returns(mockScopeFactory.Object);
        mockScopeFactory.Setup(s => s.CreateScope()).Returns(mockScope.Object);
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);

        // This will cause GetRequiredService<ContactDbContext> to throw
        mockServiceProvider.Setup(s => s.GetService(typeof(ContactDbContext))).Throws(new Exception("DB Down"));

        var service = new AuditLogBackgroundService(channel, mockServiceProvider.Object, _loggerMock.Object);

        var logEntry = new AuditLog
        {
            UserId = "fail-user",
            Action = "fail-action",
            Resource = "fail-resource",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var cts = new CancellationTokenSource();
        var executeTask = service.StartAsync(cts.Token);

        await channel.Writer.WriteAsync(logEntry);

        // Wait for retries
        await Task.Delay(1000);

        cts.Cancel();
        try { await executeTask; } catch (OperationCanceledException) { }

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Retry 1")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesProcessing_AfterPermanentFailureOfOneEntry()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<AuditLog>();

        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockScope = new Mock<IServiceScope>();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();

        mockServiceProvider.Setup(s => s.GetService(typeof(IServiceScopeFactory))).Returns(mockScopeFactory.Object);
        mockScopeFactory.Setup(s => s.CreateScope()).Returns(mockScope.Object);
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);

        // First entry will fail permanently (3 retries)
        // Second entry will succeed
        var options = new DbContextOptionsBuilder<ContactDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        var callCount = 0;
        mockServiceProvider.Setup(s => s.GetService(typeof(ContactDbContext))).Returns(() =>
        {
            callCount++;
            if (callCount <= 3) throw new Exception("Permanent DB Failure"); // First entry retries
            // Return a NEW context each time after failures to ensure it's fresh
            return new ContactDbContext(options);
        });

        var service = new AuditLogBackgroundService(channel, mockServiceProvider.Object, _loggerMock.Object);

        var logEntry1 = new AuditLog { UserId = "user1", Action = "action1", Resource = "res1", Timestamp = DateTimeOffset.UtcNow };
        var logEntry2 = new AuditLog { UserId = "user2", Action = "action2", Resource = "res2", Timestamp = DateTimeOffset.UtcNow };

        // Act
        var cts = new CancellationTokenSource();
        var executeTask = service.StartAsync(cts.Token);

        await channel.Writer.WriteAsync(logEntry1);

        // Wait for first one to fail all retries (2s + 4s + 8s ... actually it's 2^1, 2^2, 2^3)
        // retries=0 (fail) -> retry=1, delay 2^1=2s
        // retry=1 (fail) -> retry=2, delay 2^2=4s
        // retry=2 (fail) -> retry=3, stop.
        // Total delay needed: > 6s.

        await Task.Delay(7000);

        await channel.Writer.WriteAsync(logEntry2);
        await Task.Delay(1000);

        cts.Cancel();
        try { await executeTask; } catch (OperationCanceledException) { }

        // Assert
        using var checkContext = new ContactDbContext(options);
        var log2InDb = await checkContext.AuditLogs.FirstOrDefaultAsync(l => l.UserId == "user2");
        Assert.NotNull(log2InDb); // Second one should be processed
        _loggerMock.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }
}
