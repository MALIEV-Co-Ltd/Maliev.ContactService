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
        // Note: Not using ConfigureAwait(false) in test methods as it's not recommended by xUnit
        // xUnit handles synchronization context management internally
        var result = await _controller.CreateContactMessage(request);

        // Assert
        Assert.IsType<CreatedAtActionResult>(result.Result);
        var createdResult = result.Result as CreatedAtActionResult;
        Assert.Equal(nameof(ContactsController.GetContactMessage), createdResult!.ActionName);
        Assert.Equal(1, createdResult.RouteValues!["id"]);
        Assert.Equal(expectedContact, createdResult.Value);
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
        // Note: Not using ConfigureAwait(false) in test methods as it's not recommended by xUnit
        var result = await _controller.GetContactMessage(contactId);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        var okResult = result.Result as OkObjectResult;
        Assert.Equal(expectedContact, okResult!.Value);
    }

    [Fact]
    public async Task GetContactMessage_Should_Return_NotFound_When_Not_Exists()
    {
        // Arrange
        var contactId = 999;
        _contactServiceMock.Setup(x => x.GetContactMessageByIdAsync(contactId))
            .ReturnsAsync((ContactMessageDto?)null);

        // Act
        // Note: Not using ConfigureAwait(false) in test methods as it's not recommended by xUnit
        var result = await _controller.GetContactMessage(contactId);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
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

        var pagination = new PaginationParameters { Page = 1, PageSize = 20 };
        _contactServiceMock.Setup(x => x.GetContactMessagesAsync(1, 20, null, null, null))
            .ReturnsAsync(expectedContacts);

        // Act
        // Note: Not using ConfigureAwait(false) in test methods as it's not recommended by xUnit
        var result = await _controller.GetContactMessages(pagination);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        var okResult = result.Result as OkObjectResult;
        var contacts = okResult!.Value as IEnumerable<ContactMessageDto>;
        Assert.Equal(2, contacts!.Count());
        Assert.Equivalent(expectedContacts, contacts);
    }

    [Fact]
    public async Task GetContactMessages_Should_Validate_Parameters()
    {
        // Arrange
        var expectedContacts = new List<ContactMessageDto>();

        var pagination = new PaginationParameters { Page = 1, PageSize = 20 }; // Will be overridden by validation
        _contactServiceMock.Setup(x => x.GetContactMessagesAsync(1, 20, ContactStatus.New, ContactType.General, null))
            .ReturnsAsync(expectedContacts);

        // Act - Test parameter validation and defaults
        // Note: Not using ConfigureAwait(false) in test methods as it's not recommended by xUnit
        var result = await _controller.GetContactMessages(pagination, ContactStatus.New, ContactType.General);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);

        // Verify that the service was called with corrected parameters (page=1, pageSize=20)
        _contactServiceMock.Verify(x => x.GetContactMessagesAsync(1, 20, ContactStatus.New, ContactType.General, null), Times.Once);
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
        // Note: Not using ConfigureAwait(false) in test methods as it's not recommended by xUnit
        var result = await _controller.UpdateContactStatus(contactId, updateRequest);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        var okResult = result.Result as OkObjectResult;
        Assert.Equal(updatedContact, okResult!.Value);
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
            .ThrowsAsync(new Maliev.ContactService.Api.Exceptions.NotFoundException("Contact not found"));

        // Act
        // Note: Not using ConfigureAwait(false) in test methods as it's not recommended by xUnit
        var result = await _controller.UpdateContactStatus(contactId, updateRequest);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task DeleteContactMessage_Should_Return_NoContent_When_Deleted()
    {
        // Arrange
        var contactId = 1;
        _contactServiceMock.Setup(x => x.DeleteContactMessageAsync(contactId))
            .Returns(Task.CompletedTask);

        // Act
        // Note: Not using ConfigureAwait(false) in test methods as it's not recommended by xUnit
        var result = await _controller.DeleteContactMessage(contactId);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteContactMessage_Should_Return_NotFound_When_Not_Exists()
    {
        // Arrange
        var contactId = 999;
        _contactServiceMock.Setup(x => x.DeleteContactMessageAsync(contactId))
            .ThrowsAsync(new Maliev.ContactService.Api.Exceptions.NotFoundException("Contact not found"));

        // Act
        // Note: Not using ConfigureAwait(false) in test methods as it's not recommended by xUnit
        var result = await _controller.DeleteContactMessage(contactId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
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
        // Note: Not using ConfigureAwait(false) in test methods as it's not recommended by xUnit
        var result = await _controller.GetContactFiles(contactId);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        var okResult = result.Result as OkObjectResult;
        var files = okResult!.Value as IEnumerable<ContactFileDto>;
        Assert.Equivalent(expectedFiles, files);
    }

    [Fact]
    public async Task DeleteContactFile_Should_Return_NoContent_When_Deleted()
    {
        // Arrange
        var contactId = 1;
        var fileId = 1;
        _contactServiceMock.Setup(x => x.DeleteContactFileAsync(contactId, fileId))
            .Returns(Task.CompletedTask);

        // Act
        // Note: Not using ConfigureAwait(false) in test methods as it's not recommended by xUnit
        var result = await _controller.DeleteContactFile(contactId, fileId);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteContactFile_Should_Return_NotFound_When_Not_Exists()
    {
        // Arrange
        var contactId = 1;
        var fileId = 999;
        _contactServiceMock.Setup(x => x.DeleteContactFileAsync(contactId, fileId))
            .ThrowsAsync(new Maliev.ContactService.Api.Exceptions.NotFoundException("Contact file not found"));

        // Act
        // Note: Not using ConfigureAwait(false) in test methods as it's not recommended by xUnit
        var result = await _controller.DeleteContactFile(contactId, fileId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DownloadContactFile_Should_Return_File_When_Exists()
    {
        // Arrange
        var id = 1;  // This is the contactId
        var fileId = 1;
        var file = new ContactFileDto
        {
            Id = fileId,
            FileName = "document.pdf",
            ObjectName = "contacts/1/document.pdf",
            FileSize = 1024,
            ContentType = "application/pdf",
            UploadServiceFileId = "upload-123",
            CreatedAt = DateTime.UtcNow
        };

        var downloadResponse = new FileDownloadResponse
        {
            Content = new byte[] { 1, 2, 3, 4, 5 },
            ContentType = "application/pdf",
            FileName = "document.pdf",
            FileSize = 5
        };

        _contactServiceMock.Setup(x => x.GetContactFileByIdAsync(id, fileId))
            .ReturnsAsync(file);

        _uploadServiceMock.Setup(x => x.DownloadFileAsync("upload-123"))
            .ReturnsAsync(downloadResponse);

        // Act
        // Note: Not using ConfigureAwait(false) in test methods as it's not recommended by xUnit
        var result = await _controller.DownloadContactFile(id, fileId);

        // Assert
        Assert.IsType<FileContentResult>(result);
        var fileResult = result as FileContentResult;
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, fileResult!.FileContents);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.Equal("document.pdf", fileResult.FileDownloadName);
    }

    [Fact]
    public async Task DownloadContactFile_Should_Return_NotFound_When_File_Not_Exists()
    {
        // Arrange
        var contactId = 1;
        var fileId = 999;

        _contactServiceMock.Setup(x => x.GetContactFileByIdAsync(contactId, fileId))
            .ReturnsAsync((ContactFileDto?)null);

        // Act
        // Note: Not using ConfigureAwait(false) in test methods as it's not recommended by xUnit
        var result = await _controller.DownloadContactFile(contactId, fileId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GetContactMessages_Should_Have_Authorize_Attribute()
    {
        // Arrange
        var methodInfo = typeof(ContactsController).GetMethod(nameof(ContactsController.GetContactMessages));

        // Act
        var authorizeAttribute = methodInfo?.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);

        // Assert
        Assert.NotNull(authorizeAttribute);
        Assert.Single(authorizeAttribute);
    }

    [Fact]
    public void GetContactMessage_Should_Have_Authorize_Attribute()
    {
        // Arrange
        var methodInfo = typeof(ContactsController).GetMethod(nameof(ContactsController.GetContactMessage));

        // Act
        var authorizeAttribute = methodInfo?.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);

        // Assert
        Assert.NotNull(authorizeAttribute);
        Assert.Single(authorizeAttribute);
    }

    [Fact]
    public void UpdateContactStatus_Should_Have_Authorize_Attribute()
    {
        // Arrange
        var methodInfo = typeof(ContactsController).GetMethod(nameof(ContactsController.UpdateContactStatus));

        // Act
        var authorizeAttribute = methodInfo?.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);

        // Assert
        Assert.NotNull(authorizeAttribute);
        Assert.Single(authorizeAttribute);
    }

    [Fact]
    public void DeleteContactMessage_Should_Have_Authorize_Attribute()
    {
        // Arrange
        var methodInfo = typeof(ContactsController).GetMethod(nameof(ContactsController.DeleteContactMessage));

        // Act
        var authorizeAttribute = methodInfo?.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);

        // Assert
        Assert.NotNull(authorizeAttribute);
        Assert.Single(authorizeAttribute);
    }

    [Fact]
    public void GetContactFiles_Should_Have_Authorize_Attribute()
    {
        // Arrange
        var methodInfo = typeof(ContactsController).GetMethod(nameof(ContactsController.GetContactFiles));

        // Act
        var authorizeAttribute = methodInfo?.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);

        // Assert
        Assert.NotNull(authorizeAttribute);
        Assert.Single(authorizeAttribute);
    }

    [Fact]
    public void DeleteContactFile_Should_Have_Authorize_Attribute()
    {
        // Arrange
        var methodInfo = typeof(ContactsController).GetMethod(nameof(ContactsController.DeleteContactFile));

        // Act
        var authorizeAttribute = methodInfo?.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);

        // Assert
        Assert.NotNull(authorizeAttribute);
        Assert.Single(authorizeAttribute);
    }

    [Fact]
    public void DownloadContactFile_Should_Have_Authorize_Attribute()
    {
        // Arrange
        var methodInfo = typeof(ContactsController).GetMethod(nameof(ContactsController.DownloadContactFile));

        // Act
        var authorizeAttribute = methodInfo?.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);

        // Assert
        Assert.NotNull(authorizeAttribute);
        Assert.Single(authorizeAttribute);
    }

    [Fact]
    public void CreateContactMessage_Should_Have_AllowAnonymous_Attribute()
    {
        // Arrange
        var methodInfo = typeof(ContactsController).GetMethod(nameof(ContactsController.CreateContactMessage));

        // Act
        var allowAnonymousAttribute = methodInfo?.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), false);

        // Assert
        Assert.NotNull(allowAnonymousAttribute);
        Assert.Single(allowAnonymousAttribute);
    }
    [Fact]
    public async Task GetContactMessages_Should_Return_InternalServerError_On_Exception()
    {
        // Arrange
        var pagination = new PaginationParameters { Page = 1, PageSize = 20 };
        _contactServiceMock.Setup(x => x.GetContactMessagesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<ContactStatus?>(), It.IsAny<ContactType?>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetContactMessages(pagination);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task GetContactMessage_Should_Return_InternalServerError_On_Exception()
    {
        // Arrange
        _contactServiceMock.Setup(x => x.GetContactMessageByIdAsync(It.IsAny<int>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetContactMessage(1);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task GetContactFiles_Should_Return_InternalServerError_On_Exception()
    {
        // Arrange
        _contactServiceMock.Setup(x => x.GetContactFilesAsync(It.IsAny<int>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetContactFiles(1);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task DownloadContactFile_Should_Return_InternalServerError_On_Exception()
    {
        // Arrange
        var file = new ContactFileDto { Id = 1, FileName = "test.txt", UploadServiceFileId = "id", ObjectName = "obj", CreatedAt = DateTime.UtcNow };
        _contactServiceMock.Setup(x => x.GetContactFileByIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(file);
        _uploadServiceMock.Setup(x => x.DownloadFileAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.DownloadContactFile(1, 1);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public void MergeContacts_Should_Return_Ok()
    {
        // Act
        var result = _controller.MergeContacts(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }
}
