using FluentAssertions;
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
        request.FullName.Should().Be("Test User");
        request.Email.Should().Be("test@example.com");
        request.Subject.Should().Be("Test Subject");
        request.Message.Should().Be("Test Message");
        request.ContactType.Should().Be(ContactType.General);
        request.Priority.Should().Be(Priority.Medium);
        request.Files.Should().NotBeNull();
        request.Files.Should().BeEmpty();
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
        contact.ContactType.Should().Be(ContactType.General);
        contact.Priority.Should().Be(Priority.Medium);
        contact.Status.Should().Be(ContactStatus.New);
        contact.Files.Should().NotBeNull();
        contact.Files.Should().BeEmpty();
        contact.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        contact.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        contact.ResolvedAt.Should().BeNull();
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
        file.ContactMessageId.Should().Be(1);
        file.FileName.Should().Be("test.pdf");
        file.ObjectName.Should().Be("contacts/1/test.pdf");
        file.ContentType.Should().Be("application/pdf");
        file.FileSize.Should().BeNull();
        file.UploadServiceFileId.Should().BeNull();
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
        request.Status.Should().Be(ContactStatus.InProgress);
        request.Priority.Should().BeNull();
    }

    [Theory]
    [InlineData(ContactType.General)]
    [InlineData(ContactType.Business)]
    [InlineData(ContactType.Quotation)]
    [InlineData(ContactType.Supplier)]
    public void ContactType_Enum_Should_Have_Valid_Values(ContactType contactType)
    {
        // Assert
        Enum.IsDefined(typeof(ContactType), contactType).Should().BeTrue();
    }

    [Theory]
    [InlineData(Priority.Low)]
    [InlineData(Priority.Medium)]
    [InlineData(Priority.High)]
    [InlineData(Priority.Urgent)]
    public void Priority_Enum_Should_Have_Valid_Values(Priority priority)
    {
        // Assert
        Enum.IsDefined(typeof(Priority), priority).Should().BeTrue();
    }

    [Theory]
    [InlineData(ContactStatus.New)]
    [InlineData(ContactStatus.InProgress)]
    [InlineData(ContactStatus.Resolved)]
    [InlineData(ContactStatus.Closed)]
    public void ContactStatus_Enum_Should_Have_Valid_Values(ContactStatus status)
    {
        // Assert
        Enum.IsDefined(typeof(ContactStatus), status).Should().BeTrue();
    }
}