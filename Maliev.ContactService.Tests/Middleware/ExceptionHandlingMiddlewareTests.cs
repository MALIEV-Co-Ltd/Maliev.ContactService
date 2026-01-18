using System.Net;
using System.Text.Json;
using Maliev.ContactService.Api.Exceptions;
using Maliev.ContactService.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.ContactService.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _loggerMock;
    private readonly DefaultHttpContext _context;

    public ExceptionHandlingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        _context = new DefaultHttpContext();
        // Set a dummy response stream to capture output
        _context.Response.Body = new MemoryStream();
    }

    [Fact]
    public async Task InvokeAsync_WhenNoException_CallsNext()
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(_context);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_WhenNotFoundException_ReturnsNotFound()
    {
        // Arrange
        RequestDelegate next = (ctx) => throw new NotFoundException("Not found message");
        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(_context);

        // Assert
        Assert.Equal((int)HttpStatusCode.NotFound, _context.Response.StatusCode);

        _context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        var problem = JsonDocument.Parse(responseBody);

        Assert.Equal("Not Found", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal("Not found message", problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_WhenDuplicateInquiryException_ReturnsConflict()
    {
        // Arrange
        RequestDelegate next = (ctx) => throw new DuplicateInquiryException("Duplicate message");
        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(_context);

        // Assert
        Assert.Equal((int)HttpStatusCode.Conflict, _context.Response.StatusCode);

        _context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        var problem = JsonDocument.Parse(responseBody);

        Assert.Equal("Conflict", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal("Duplicate message", problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_WhenCountryServiceException_ReturnsServiceUnavailable()
    {
        // Arrange
        RequestDelegate next = (ctx) => throw new CountryServiceException("Service unavailable message");
        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(_context);

        // Assert
        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, _context.Response.StatusCode);

        _context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        var problem = JsonDocument.Parse(responseBody);

        Assert.Equal("Service Unavailable", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal("Service unavailable message", problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_WhenInvalidOperationException_ReturnsConflict()
    {
        // Arrange
        RequestDelegate next = (ctx) => throw new InvalidOperationException("Conflict message");
        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(_context);

        // Assert
        Assert.Equal((int)HttpStatusCode.Conflict, _context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledException_ReturnsInternalServerError()
    {
        // Arrange
        RequestDelegate next = (ctx) => throw new Exception("Boom");
        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(_context);

        // Assert
        Assert.Equal((int)HttpStatusCode.InternalServerError, _context.Response.StatusCode);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unhandled exception occurred")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenAggregateException_UnwrapsAndHandles()
    {
        // Arrange
        RequestDelegate next = (ctx) => throw new AggregateException(new NotFoundException("Inner not found"));
        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(_context);

        // Assert
        Assert.Equal((int)HttpStatusCode.NotFound, _context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenArgumentException_ReturnsBadRequest()
    {
        // Arrange
        RequestDelegate next = (ctx) => throw new ArgumentException("Bad argument");
        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(_context);

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, _context.Response.StatusCode);
    }
}
