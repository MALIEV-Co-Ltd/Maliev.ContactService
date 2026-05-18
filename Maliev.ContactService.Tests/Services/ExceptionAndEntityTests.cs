using Maliev.ContactService.Application.Exceptions;
using Maliev.ContactService.Application.Interfaces;
using Maliev.ContactService.Application.Services;
using Maliev.ContactService.Domain.Entities;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Xunit;
using Moq;

namespace Maliev.ContactService.Tests.Services;

public class ExceptionTests
{
    [Fact]
    public void NotFoundException_WithMessage_SetsMessage()
    {
        var message = "Test message";
        var exception = new NotFoundException(message);

        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void DuplicateInquiryException_WithDefaultConstructor_HasDefaultMessage()
    {
        var exception = new DuplicateInquiryException();

        Assert.NotNull(exception.Message);
    }

    [Fact]
    public void DuplicateInquiryException_WithMessage_SetsMessage()
    {
        var message = "Duplicate inquiry";
        var exception = new DuplicateInquiryException(message);

        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void CountryServiceException_WithDefaultConstructor_HasDefaultMessage()
    {
        var exception = new CountryServiceException();

        Assert.NotNull(exception.Message);
    }

    [Fact]
    public void CountryServiceException_WithMessage_SetsMessage()
    {
        var message = "Country service unavailable";
        var exception = new CountryServiceException(message);

        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void CountryServiceException_WithMessageAndInnerException_SetsProperties()
    {
        var message = "Country service error";
        var innerException = new Exception("Network error");
        var exception = new CountryServiceException(message, innerException);

        Assert.Equal(message, exception.Message);
        Assert.Equal(innerException, exception.InnerException);
    }
}

public class AuditLogServiceTests
{
    [Fact]
    public void LogDecision_WithValidData_WritesToChannel()
    {
        var channel = Channel.CreateUnbounded<AuditLog>();
        var service = new AuditLogService(channel);

        service.LogDecision("user1", "test.action", "resource1", true, "reason", "127.0.0.1");

        Assert.True(channel.Reader.TryRead(out var log));
        Assert.Equal("user1", log.UserId);
        Assert.Equal("test.action", log.Action);
        Assert.Equal("resource1", log.Resource);
        Assert.True(log.Result);
    }

    [Fact]
    public void LogDecision_WithNullOptionalParams_SetsNullValues()
    {
        var channel = Channel.CreateUnbounded<AuditLog>();
        var service = new AuditLogService(channel);

        service.LogDecision("user1", "test.action", "resource1", true, null, null);

        Assert.True(channel.Reader.TryRead(out var log));
        Assert.Null(log.Reason);
        Assert.Null(log.ClientIp);
    }

    [Fact]
    public void LogDecision_WithFalseResult_SetsResultFalse()
    {
        var channel = Channel.CreateUnbounded<AuditLog>();
        var service = new AuditLogService(channel);

        service.LogDecision("user1", "test.action", "resource1", false, null, null);

        Assert.True(channel.Reader.TryRead(out var log));
        Assert.False(log.Result);
    }
}

public class InfrastructureServiceTests
{
    [Fact]
    public void UploadResponse_Properties_CanBeRead()
    {
        var response = new UploadResponse("file123", 1024);

        Assert.Equal("file123", response.FileId);
        Assert.Equal(1024, response.FileSize);
    }

    [Fact]
    public void DownloadResponse_Properties_CanBeRead()
    {
        var content = new byte[] { 1, 2, 3 };
        var response = new DownloadResponse(content, "text/plain", "test.txt");

        Assert.Equal(content, response.Content);
        Assert.Equal("text/plain", response.ContentType);
        Assert.Equal("test.txt", response.FileName);
    }

    [Fact]
    public void UploadResponse_FileId_MatchesConstructor()
    {
        var response = new UploadResponse("test-id-123", 2048);

        Assert.Equal("test-id-123", response.FileId);
    }

    [Fact]
    public void DownloadResponse_DefaultValues()
    {
        var response = new DownloadResponse(Array.Empty<byte>(), "application/octet-stream", "default.bin");

        Assert.Empty(response.Content);
        Assert.Equal("application/octet-stream", response.ContentType);
        Assert.Equal("default.bin", response.FileName);
    }

    [Fact]
    public void DependencyInjection_UploadServiceHttpClient_UsesShortTimeout()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Maliev.ContactService.Infrastructure",
            "DependencyInjection.cs"));

        Assert.True(File.Exists(sourcePath), $"Could not find source file: {sourcePath}");
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("client.Timeout = TimeSpan.FromSeconds(5);", source, StringComparison.Ordinal);
    }
}

public class EntityTests
{
    [Fact]
    public void ContactMessage_DefaultValues_AreSet()
    {
        var contact = new ContactMessage
        {
            FullName = "Test",
            Email = "test@test.com",
            Subject = "Subject",
            Message = "Message",
            CountryId = Guid.Empty
        };

        Assert.Equal(ContactStatus.New, contact.Status);
        Assert.Equal(Priority.Medium, contact.Priority);
        Assert.Equal(ContactType.General, contact.ContactType);
    }

    [Fact]
    public void ContactFile_DefaultValues_AreSet()
    {
        var file = new ContactFile
        {
            ContactMessageId = 1,
            FileName = "test.txt",
            ObjectName = "test"
        };

        Assert.NotEqual(default, file.CreatedAt);
        Assert.NotEqual(default, file.UpdatedAt);
    }

    [Fact]
    public void AuditLog_DefaultValues_AreSet()
    {
        var auditLog = new AuditLog
        {
            UserId = "user1",
            Action = "TestAction",
            Resource = "TestResource",
            Result = true
        };

        Assert.NotEqual(default, auditLog.Timestamp);
    }
}
