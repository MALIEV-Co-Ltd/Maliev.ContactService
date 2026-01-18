using Maliev.ContactService.Api.Middleware;
using Maliev.ContactService.Api.Services.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;

namespace Maliev.ContactService.Tests.Middleware;

public class AuditLogMiddlewareTests
{
    private readonly Mock<ILogger<AuditLogMiddleware>> _loggerMock;
    private readonly Mock<IAuditLogService> _auditLogServiceMock;
    private readonly DefaultHttpContext _context;

    public AuditLogMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<AuditLogMiddleware>>();
        _auditLogServiceMock = new Mock<IAuditLogService>();
        _context = new DefaultHttpContext();
    }

    [Fact]
    public async Task InvokeAsync_WhenSuccess_DoesNotLog()
    {
        // Arrange
        RequestDelegate next = (ctx) =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };
        var middleware = new AuditLogMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(_context, _auditLogServiceMock.Object);

        // Assert
        _auditLogServiceMock.Verify(x => x.LogDecision(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_When401_LogsUnauthorized()
    {
        // Arrange
        RequestDelegate next = (ctx) =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        var middleware = new AuditLogMiddleware(next, _loggerMock.Object);
        _context.Request.Method = "GET";
        _context.Request.Path = "/test";

        // Act
        await middleware.InvokeAsync(_context, _auditLogServiceMock.Object);

        // Assert
        _auditLogServiceMock.Verify(x => x.LogDecision(
            "anonymous",
            "GET /test",
            "/test",
            false,
            "Unauthorized (Missing or invalid token)",
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_When403_LogsForbidden()
    {
        // Arrange
        RequestDelegate next = (ctx) =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
        var middleware = new AuditLogMiddleware(next, _loggerMock.Object);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "user-123") };
        _context.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
        _context.Request.Method = "POST";
        _context.Request.Path = "/test";

        // Act
        await middleware.InvokeAsync(_context, _auditLogServiceMock.Object);

        // Assert
        _auditLogServiceMock.Verify(x => x.LogDecision(
            "user-123",
            "POST /test",
            "/test",
            false,
            "Forbidden",
            It.IsAny<string>()), Times.Once);
    }
}
