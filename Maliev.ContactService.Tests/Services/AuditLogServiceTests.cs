using Maliev.ContactService.Api.Services.Auth;
using Maliev.ContactService.Data.Models;
using System.Threading.Channels;
using Xunit;

namespace Maliev.ContactService.Tests.Services;

public class AuditLogServiceTests
{
    [Fact]
    public async Task LogDecision_WritesToChannel()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<AuditLog>();
        var service = new AuditLogService(channel);

        // Act
        service.LogDecision("user", "act", "res", true, "reason", "127.0.0.1");

        // Assert
        Assert.True(channel.Reader.TryRead(out var log));
        Assert.Equal("user", log.UserId);
        Assert.Equal("act", log.Action);
    }

    [Fact]
    public void LogDecision_HandlesExceptionSilently()
    {
        // Arrange
        // Passing null channel might cause exception depending on implementation,
        // but here it's used in constructor.
        // Let's mock a channel that throws on TryWrite if possible,
        // or just pass null to constructor if it's not checked.

        var service = new AuditLogService(null!); // This will throw NullReferenceException in LogDecision

        // Act & Assert
        var exception = Record.Exception(() => service.LogDecision("user", "act", "res", true, "reason", "127.0.0.1"));
        Assert.Null(exception); // Should be caught and ignored
    }
}
