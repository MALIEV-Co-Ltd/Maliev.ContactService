using FluentAssertions;
using Maliev.ContactService.Api.Controllers;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.ContactService.Tests.Controllers;

public class ContactsControllerTests
{
    private readonly Mock<IContactService> _contactServiceMock;
    private readonly Mock<IUploadServiceClient> _uploadServiceMock;
    private readonly Mock<ILogger<ContactsController>> _loggerMock;
    private readonly ContactsController _controller;

    public ContactsControllerTests()
    {
        _contactServiceMock = new Mock<IContactService>();
        _uploadServiceMock = new Mock<IUploadServiceClient>();
        _loggerMock = new Mock<ILogger<ContactsController>>();
        _controller = new ContactsController(_contactServiceMock.Object, _uploadServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateContactMessage_Should_Return_Created_Result()
    {
        // Arrange
        var request = new CreateContactMessageRequest
        {
            FullName = "John Doe",
            Email = "john.doe@example.com",
            Subject = "Test Subject",
            Message = "Test Message",
            ContactType = ContactType.General
        };

        var expectedContact = new ContactMessageDto
        {
            Id = 1,
            FullName = "John Doe",
            Email = "john.doe@example.com",
            Subject = "Test Subject",
            Message = "Test Message",
            ContactType = ContactType.General,
            Priority = Priority.Medium,
            Status = ContactStatus.New,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _contactServiceMock.Setup(x => x.CreateContactMessageAsync(request))
            .ReturnsAsync(expectedContact);

        // Act
        var result = await _controller.CreateContactMessage(request);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result.Result as CreatedAtActionResult;
        createdResult!.ActionName.Should().Be(nameof(ContactsController.GetContactMessage));
        createdResult.RouteValues!["id"].Should().Be(1);
        createdResult.Value.Should().Be(expectedContact);
    }

    [Fact]
    public async Task CreateContactMessage_Should_Handle_Exception()
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

        _contactServiceMock.Setup(x => x.CreateContactMessageAsync(request))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _controller.CreateContactMessage(request);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>();
        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GetContactMessage_Should_Return_Contact_When_Exists()
    {
        // Arrange
        var contactId = 1;
        var expectedContact = new ContactMessageDto
        {
            Id = contactId,
            FullName = "John Doe",
            Email = "john.doe@example.com",
            Subject = "Test Subject",
            Message = "Test Message",
            ContactType = ContactType.General,
            Priority = Priority.Medium,
            Status = ContactStatus.New,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _contactServiceMock.Setup(x => x.GetContactMessageByIdAsync(contactId))
            .ReturnsAsync(expectedContact);

        // Act
        var result = await _controller.GetContactMessage(contactId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().Be(expectedContact);
    }

    [Fact]
    public async Task GetContactMessage_Should_Return_NotFound_When_Not_Exists()
    {
        // Arrange
        var contactId = 999;
        _contactServiceMock.Setup(x => x.GetContactMessageByIdAsync(contactId))
            .ReturnsAsync((ContactMessageDto?)null);

        // Act
        var result = await _controller.GetContactMessage(contactId);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetContactMessages_Should_Return_Paginated_Results()
    {
        // Arrange
        var expectedContacts = new List<ContactMessageDto>
        {
            new ContactMessageDto
            {
                Id = 1,
                FullName = "User 1",
                Email = "user1@example.com",
                Subject = "Subject 1",
                Message = "Message 1",
                ContactType = ContactType.General,
                Priority = Priority.Medium,
                Status = ContactStatus.New,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ContactMessageDto
            {
                Id = 2,
                FullName = "User 2",
                Email = "user2@example.com",
                Subject = "Subject 2",
                Message = "Message 2",
                ContactType = ContactType.Business,
                Priority = Priority.High,
                Status = ContactStatus.InProgress,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _contactServiceMock.Setup(x => x.GetContactMessagesAsync(1, 20, null, null))
            .ReturnsAsync(expectedContacts);

        // Act
        var result = await _controller.GetContactMessages();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var contacts = okResult!.Value as IEnumerable<ContactMessageDto>;
        contacts.Should().HaveCount(2);
        contacts.Should().BeEquivalentTo(expectedContacts);
    }

    [Fact]
    public async Task GetContactMessages_Should_Validate_Parameters()
    {
        // Arrange
        var expectedContacts = new List<ContactMessageDto>();

        _contactServiceMock.Setup(x => x.GetContactMessagesAsync(1, 20, ContactStatus.New, ContactType.General))
            .ReturnsAsync(expectedContacts);

        // Act - Test parameter validation and defaults
        var result = await _controller.GetContactMessages(page: -1, pageSize: 150, status: ContactStatus.New, contactType: ContactType.General);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        // Verify that the service was called with corrected parameters (page=1, pageSize=20)
        _contactServiceMock.Verify(x => x.GetContactMessagesAsync(1, 20, ContactStatus.New, ContactType.General), Times.Once);
    }

    [Fact]
    public async Task UpdateContactStatus_Should_Return_Updated_Contact()
    {
        // Arrange
        var contactId = 1;
        var updateRequest = new UpdateContactStatusRequest
        {
            Status = ContactStatus.InProgress,
            Priority = Priority.High
        };

        var updatedContact = new ContactMessageDto
        {
            Id = contactId,
            FullName = "John Doe",
            Email = "john.doe@example.com",
            Subject = "Test Subject",
            Message = "Test Message",
            ContactType = ContactType.General,
            Priority = Priority.High,
            Status = ContactStatus.InProgress,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow
        };

        _contactServiceMock.Setup(x => x.UpdateContactStatusAsync(contactId, updateRequest))
            .ReturnsAsync(updatedContact);

        // Act
        var result = await _controller.UpdateContactStatus(contactId, updateRequest);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().Be(updatedContact);
    }

    [Fact]
    public async Task UpdateContactStatus_Should_Return_NotFound_When_Contact_Not_Exists()
    {
        // Arrange
        var contactId = 999;
        var updateRequest = new UpdateContactStatusRequest
        {
            Status = ContactStatus.InProgress
        };

        _contactServiceMock.Setup(x => x.UpdateContactStatusAsync(contactId, updateRequest))
            .ReturnsAsync((ContactMessageDto?)null);

        // Act
        var result = await _controller.UpdateContactStatus(contactId, updateRequest);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteContactMessage_Should_Return_NoContent_When_Deleted()
    {
        // Arrange
        var contactId = 1;
        _contactServiceMock.Setup(x => x.DeleteContactMessageAsync(contactId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteContactMessage(contactId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteContactMessage_Should_Return_NotFound_When_Not_Exists()
    {
        // Arrange
        var contactId = 999;
        _contactServiceMock.Setup(x => x.DeleteContactMessageAsync(contactId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteContactMessage(contactId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetContactFiles_Should_Return_Files()
    {
        // Arrange
        var contactId = 1;
        var expectedFiles = new List<ContactFileDto>
        {
            new ContactFileDto
            {
                Id = 1,
                FileName = "document1.pdf",
                ObjectName = "contacts/1/document1.pdf",
                FileSize = 1024,
                ContentType = "application/pdf",
                UploadServiceFileId = "upload-123",
                CreatedAt = DateTime.UtcNow
            }
        };

        _contactServiceMock.Setup(x => x.GetContactFilesAsync(contactId))
            .ReturnsAsync(expectedFiles);

        // Act
        var result = await _controller.GetContactFiles(contactId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var files = okResult!.Value as IEnumerable<ContactFileDto>;
        files.Should().BeEquivalentTo(expectedFiles);
    }

    [Fact]
    public async Task DeleteContactFile_Should_Return_NoContent_When_Deleted()
    {
        // Arrange
        var contactId = 1;
        var fileId = 1;
        _contactServiceMock.Setup(x => x.DeleteContactFileAsync(contactId, fileId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteContactFile(contactId, fileId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteContactFile_Should_Return_NotFound_When_Not_Exists()
    {
        // Arrange
        var contactId = 1;
        var fileId = 999;
        _contactServiceMock.Setup(x => x.DeleteContactFileAsync(contactId, fileId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteContactFile(contactId, fileId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DownloadContactFile_Should_Return_File_When_Exists()
    {
        // Arrange
        var contactId = 1;
        var fileId = 1;
        var files = new List<ContactFileDto>
        {
            new ContactFileDto
            {
                Id = fileId,
                FileName = "document.pdf",
                ObjectName = "contacts/1/document.pdf",
                FileSize = 1024,
                ContentType = "application/pdf",
                UploadServiceFileId = "upload-123",
                CreatedAt = DateTime.UtcNow
            }
        };

        var downloadResponse = new FileDownloadResponse
        {
            Content = new byte[] { 1, 2, 3, 4, 5 },
            ContentType = "application/pdf",
            FileName = "document.pdf",
            FileSize = 5
        };

        _contactServiceMock.Setup(x => x.GetContactFilesAsync(contactId))
            .ReturnsAsync(files);

        _uploadServiceMock.Setup(x => x.DownloadFileAsync("upload-123"))
            .ReturnsAsync(downloadResponse);

        // Act
        var result = await _controller.DownloadContactFile(contactId, fileId);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        var fileResult = result as FileContentResult;
        fileResult!.FileContents.Should().BeEquivalentTo(new byte[] { 1, 2, 3, 4, 5 });
        fileResult.ContentType.Should().Be("application/pdf");
        fileResult.FileDownloadName.Should().Be("document.pdf");
    }

    [Fact]
    public async Task DownloadContactFile_Should_Return_NotFound_When_File_Not_Exists()
    {
        // Arrange
        var contactId = 1;
        var fileId = 999;
        var files = new List<ContactFileDto>();

        _contactServiceMock.Setup(x => x.GetContactFilesAsync(contactId))
            .ReturnsAsync(files);

        // Act
        var result = await _controller.DownloadContactFile(contactId, fileId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DownloadContactFile_Should_Handle_Upload_Service_Exception()
    {
        // Arrange
        var contactId = 1;
        var fileId = 1;
        var files = new List<ContactFileDto>
        {
            new ContactFileDto
            {
                Id = fileId,
                FileName = "document.pdf",
                ObjectName = "contacts/1/document.pdf",
                FileSize = 1024,
                ContentType = "application/pdf",
                UploadServiceFileId = "upload-123",
                CreatedAt = DateTime.UtcNow
            }
        };

        _contactServiceMock.Setup(x => x.GetContactFilesAsync(contactId))
            .ReturnsAsync(files);

        _uploadServiceMock.Setup(x => x.DownloadFileAsync("upload-123"))
            .ThrowsAsync(new HttpRequestException("Upload service unavailable"));

        // Act
        var result = await _controller.DownloadContactFile(contactId, fileId);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
    }
}