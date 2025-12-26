using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Data.Models;

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
}
