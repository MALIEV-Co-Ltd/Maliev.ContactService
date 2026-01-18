using System.ComponentModel.DataAnnotations;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Data.Models;
using Xunit;

namespace Maliev.ContactService.Tests.Models;

public class ModelTests
{
    [Fact]
    public void CreateContactMessageRequest_Should_Have_Required_Properties()
    {
        // Arrange
        var request = new CreateContactMessageRequest
        {
            FullName = "Test User",
            Email = "test@example.com",
            Subject = "Test Subject",
            Message = "Test Message",
            ContactType = ContactType.General
        };

        // Assert
        Assert.Equal("Test User", request.FullName);
        Assert.Equal("test@example.com", request.Email);
        Assert.Equal("Test Subject", request.Subject);
        Assert.Equal("Test Message", request.Message);
        Assert.Equal(ContactType.General, request.ContactType);
        Assert.Equal(Priority.Medium, request.Priority);
        Assert.NotNull(request.Files);
        Assert.Empty(request.Files);
    }

    [Fact]
    public void ContactMessage_Should_Initialize_With_Defaults()
    {
        // Arrange & Act
        var contact = new ContactMessage
        {
            FullName = "Test User",
            Email = "test@example.com",
            Subject = "Test Subject",
            Message = "Test Message"
        };

        // Assert
        Assert.Equal(ContactType.General, contact.ContactType);
        Assert.Equal(Priority.Medium, contact.Priority);
        Assert.Equal(ContactStatus.New, contact.Status);
        Assert.NotNull(contact.Files);
        Assert.Empty(contact.Files);
        Assert.True((DateTime.UtcNow - contact.CreatedAt).Duration() < TimeSpan.FromSeconds(5));
        Assert.True((DateTime.UtcNow - contact.UpdatedAt).Duration() < TimeSpan.FromSeconds(5));
        Assert.Null(contact.ResolvedAt);
    }

    [Fact]
    public void ContactFile_Should_Have_Required_Properties()
    {
        // Arrange & Act
        var file = new ContactFile
        {
            ContactMessageId = 1,
            FileName = "test.pdf",
            ObjectName = "contacts/1/test.pdf",
            ContentType = "application/pdf",
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(1, file.ContactMessageId);
        Assert.Equal("test.pdf", file.FileName);
        Assert.Equal("contacts/1/test.pdf", file.ObjectName);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Null(file.FileSize);
        Assert.Null(file.UploadServiceFileId);
    }

    [Fact]
    public void UpdateContactStatusRequest_Should_Allow_Optional_Priority()
    {
        // Arrange & Act
        var request = new UpdateContactStatusRequest
        {
            Status = ContactStatus.InProgress
        };

        // Assert
        Assert.Equal(ContactStatus.InProgress, request.Status);
        Assert.Null(request.Priority);
    }

    [Theory]
    [InlineData(ContactType.General)]
    [InlineData(ContactType.Business)]
    [InlineData(ContactType.Supplier)]
    public void ContactType_Enum_Should_Have_Valid_Values(ContactType contactType)
    {
        // Assert
        Assert.True(Enum.IsDefined(typeof(ContactType), contactType));
    }

    [Theory]
    [InlineData(Priority.Low)]
    [InlineData(Priority.Medium)]
    [InlineData(Priority.High)]
    [InlineData(Priority.Urgent)]
    public void Priority_Enum_Should_Have_Valid_Values(Priority priority)
    {
        // Assert
        Assert.True(Enum.IsDefined(typeof(Priority), priority));
    }

    [Theory]
    [InlineData(ContactStatus.New)]
    [InlineData(ContactStatus.InProgress)]
    [InlineData(ContactStatus.Resolved)]
    [InlineData(ContactStatus.Closed)]
    public void ContactStatus_Enum_Should_Have_Valid_Values(ContactStatus status)
    {
        // Assert
        Assert.True(Enum.IsDefined(typeof(ContactStatus), status));
    }

    [Fact]
    public void CreateContactMessageRequest_Validate_InvalidContactType_ReturnsError()
    {
        // Arrange
        var request = new CreateContactMessageRequest
        {
            FullName = "Test",
            Email = "test@test.com",
            Subject = "Test",
            Message = "Test",
            ContactType = (ContactType)999
        };
        var context = new ValidationContext(request);

        // Act
        var results = request.Validate(context);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateContactMessageRequest.ContactType)));
    }

    [Fact]
    public void CreateContactMessageRequest_Validate_TooManyFiles_ReturnsError()
    {
        // Arrange
        var request = new CreateContactMessageRequest
        {
            FullName = "Test",
            Email = "test@test.com",
            Subject = "Test",
            Message = "Test",
            Files = Enumerable.Range(0, 11).Select(i => new CreateContactFileRequest { FileName = "test.txt", FileContent = new byte[10] }).ToList()
        };
        var context = new ValidationContext(request);

        // Act
        var results = request.Validate(context);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateContactMessageRequest.Files)));
    }

    [Fact]
    public void CreateContactMessageRequest_Validate_FileTooLarge_ReturnsError()
    {
        // Arrange
        var request = new CreateContactMessageRequest
        {
            FullName = "Test",
            Email = "test@test.com",
            Subject = "Test",
            Message = "Test",
            Files = new List<CreateContactFileRequest>
            {
                new CreateContactFileRequest
                {
                    FileName = "large.txt",
                    FileContent = new byte[26 * 1024 * 1024] // 26MB
                }
            }
        };
        var context = new ValidationContext(request);

        // Act
        var results = request.Validate(context);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("Files[0]"));
    }

    [Fact]
    public void CacheOptions_Properties_Work()
    {
        var options = new CacheOptions { MaxCacheSize = 2000, DefaultExpirationMinutes = 10 };
        Assert.Equal(2000, options.MaxCacheSize);
        Assert.Equal(10, options.DefaultExpirationMinutes);
    }

    [Fact]
    public void CountryDto_Properties_Work()
    {
        var dto = new CountryDto { Id = 1, Name = "Test", Iso2 = "TS", IsActive = true };
        Assert.Equal(1, dto.Id);
        Assert.Equal("Test", dto.Name);
        Assert.Equal("TS", dto.Iso2);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public void UploadRequest_Properties_Work()
    {
        var request = new UploadRequest
        {
            ObjectName = "obj",
            ContentType = "text/plain",
            FileName = "test.txt",
            FileContent = new byte[] { 1, 2, 3 }
        };
        Assert.Equal("obj", request.ObjectName);
        Assert.Equal("text/plain", request.ContentType);
        Assert.Equal("test.txt", request.FileName);
        Assert.Equal(3, request.FileContent.Length);
    }

    [Fact]
    public void CreateContactMessageRequest_Files_Can_Be_Null()
    {
        var request = new CreateContactMessageRequest
        {
            FullName = "T",
            Email = "e",
            Subject = "s",
            Message = "m",
            Files = null!
        };
        Assert.Null(request.Files);
    }
}
