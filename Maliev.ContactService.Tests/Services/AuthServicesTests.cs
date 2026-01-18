using Maliev.ContactService.Api.Services.Auth;
using Maliev.ContactService.Data.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Threading.Channels;
using Xunit;

namespace Maliev.ContactService.Tests.Services;

public class AuthServicesTests
{
    [Fact]
    public void AuditLogService_LogDecision_ShouldWriteToChannel()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<AuditLog>();
        var service = new AuditLogService(channel);
        var userId = "test-user";
        var action = "test-action";
        var resource = "test-resource";

        // Act
        service.LogDecision(userId, action, resource, true, "reason", "127.0.0.1");

        // Assert
        Assert.True(channel.Reader.TryRead(out var log));
        Assert.Equal(userId, log.UserId);
        Assert.Equal(action, log.Action);
        Assert.Equal(resource, log.Resource);
        Assert.True(log.Result);
        Assert.Equal("reason", log.Reason);
        Assert.Equal("127.0.0.1", log.ClientIp);
    }

    [Fact]
    public void ContactIAMRegistrationService_Constructor_ShouldWork()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        var loggerMock = new Mock<ILogger<ContactIAMRegistrationService>>();

        // Act
        var service = new ContactIAMRegistrationService(configMock.Object, loggerMock.Object);

        // Assert
        Assert.NotNull(service);
    }
}
