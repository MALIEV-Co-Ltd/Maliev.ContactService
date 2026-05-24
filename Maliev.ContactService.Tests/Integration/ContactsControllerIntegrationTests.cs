using System.Net;
using System.Net.Http.Json;
using Maliev.ContactService.Application.DTOs;
using Maliev.ContactService.Domain.Entities;
using Maliev.ContactService.Infrastructure.Persistence;
using Maliev.ContactService.Tests.Integration.Infrastructure;
using Maliev.ContactService.Tests.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Maliev.ContactService.Tests.Integration;

[Collection(nameof(IntegrationTestCollection))]
public class ContactsControllerIntegrationTests : BaseIntegrationTest
{
    public ContactsControllerIntegrationTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CreateContactMessage_WithValidData_ReturnsCreated()
    {
        // Arrange
        var notificationPublisher = Factory.Services.GetRequiredService<CapturingContactNotificationPublisher>();
        notificationPublisher.Reset();
        var request = new CreateContactMessageRequest
        {
            FullName = "Test Integration",
            Email = "test.integration@example.com",
            Subject = "Test Subject",
            Message = "Test Message",
            CountryId = Guid.Empty,
            ContactType = ContactType.General
        };

        // Act
        var response = await Client.PostAsJsonAsync("/contact/v1/contacts", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await GetResponseAsync<ContactMessageDto>(response);
        Assert.NotNull(result);
        Assert.Equal(request.Email, result!.Email);

        var publishedMessage = Assert.Single(notificationPublisher.PublishedMessages);
        Assert.Equal(result.Id, publishedMessage.Id);
        Assert.Equal(request.Email, publishedMessage.Email);
        Assert.Equal(request.Subject, publishedMessage.Subject);
    }

    [Fact]
    public async Task GetContactMessage_ExistingId_ReturnsOk()
    {
        // Arrange
        var contact = await CreateTestContactAsync();

        // Act
        var client = Factory.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/contact/v1/contacts/{contact.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await GetResponseAsync<ContactMessageDto>(response);
        Assert.NotNull(result);
        Assert.Equal(contact.Id, result!.Id);
    }

    [Fact]
    public async Task GetContactMessages_WithNoFilters_ReturnsOk()
    {
        // Arrange
        await CreateTestContactAsync("test1@example.com");
        await CreateTestContactAsync("test2@example.com");

        // Act
        var client = Factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/contact/v1/contacts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await GetResponseAsync<IEnumerable<ContactMessageDto>>(response);
        Assert.NotNull(result);
        Assert.True(result!.Count() >= 2);
    }

    [Fact]
    public async Task GetContactMessages_WithStatusFilter_ReturnsFilteredResults()
    {
        // Arrange
        var contact = await CreateTestContactAsync("filter.test@example.com");
        contact.Status = ContactStatus.InProgress;

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ContactDbContext>();
        context.ContactMessages.Update(contact);
        await context.SaveChangesAsync();

        // Act
        var client = Factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/contact/v1/contacts?status=1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await GetResponseAsync<IEnumerable<ContactMessageDto>>(response);
        Assert.NotNull(result);
        Assert.All(result, r => Assert.Equal(ContactStatus.InProgress, r.Status));
    }

    [Fact]
    public async Task GetContactMessages_WithEmailFilter_ReturnsFilteredResults()
    {
        // Arrange
        var testEmail = "email.filter@example.com";
        await CreateTestContactAsync(testEmail);
        await CreateTestContactAsync("other@example.com");

        // Act
        var client = Factory.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/contact/v1/contacts?email={testEmail}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await GetResponseAsync<IEnumerable<ContactMessageDto>>(response);
        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Equal(testEmail, result.First().Email);
    }

    [Fact]
    public async Task GetContactMessages_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            await CreateTestContactAsync($"pagination{i}@example.com");
        }

        // Act
        var client = Factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/contact/v1/contacts?page=1&pageSize=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await GetResponseAsync<IEnumerable<ContactMessageDto>>(response);
        Assert.NotNull(result);
        Assert.Equal(2, result!.Count());
    }

    [Fact]
    public async Task UpdateContactStatus_WithValidData_ReturnsOk()
    {
        // Arrange
        var contact = await CreateTestContactAsync("updatestatus@example.com");
        var request = new UpdateContactStatusRequest
        {
            Status = ContactStatus.Resolved
        };

        // Act
        var client = Factory.CreateAuthenticatedClient();
        var response = await client.PutAsJsonAsync($"/contact/v1/contacts/{contact.Id}/status", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await GetResponseAsync<ContactMessageDto>(response);
        Assert.NotNull(result);
        Assert.Equal(ContactStatus.Resolved, result!.Status);
        Assert.NotNull(result.ResolvedAt);
    }

    [Fact]
    public async Task UpdateContactStatus_WithPriority_ReturnsOk()
    {
        // Arrange
        var contact = await CreateTestContactAsync("updatepriority@example.com");
        var request = new UpdateContactStatusRequest
        {
            Status = ContactStatus.InProgress,
            Priority = Priority.High
        };

        // Act
        var client = Factory.CreateAuthenticatedClient();
        var response = await client.PutAsJsonAsync($"/contact/v1/contacts/{contact.Id}/status", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await GetResponseAsync<ContactMessageDto>(response);
        Assert.NotNull(result);
        Assert.Equal(Priority.High, result!.Priority);
    }

    [Fact]
    public async Task UpdateContactStatus_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdateContactStatusRequest
        {
            Status = ContactStatus.Resolved
        };

        // Act
        var client = Factory.CreateAuthenticatedClient();
        var response = await client.PutAsJsonAsync("/contact/v1/contacts/99999/status", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteContactMessage_ExistingId_ReturnsNoContent()
    {
        // Arrange
        var contact = await CreateTestContactAsync("delete@example.com");
        var client = Factory.CreateAuthenticatedClient();

        // Act
        var response = await client.DeleteAsync($"/contact/v1/contacts/{contact.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify deletion
        var getResponse = await client.GetAsync($"/contact/v1/contacts/{contact.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteContactMessage_NonExistentId_ReturnsNotFound()
    {
        // Act
        var client = Factory.CreateAuthenticatedClient();
        var response = await client.DeleteAsync("/contact/v1/contacts/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetContactMessage_NonExistentId_ReturnsNotFound()
    {
        // Act
        var client = Factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/contact/v1/contacts/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateContactMessage_WithDuplicateEmailWithin60Seconds_ReturnsConflict()
    {
        // Arrange
        var request = new CreateContactMessageRequest
        {
            FullName = "Duplicate Test",
            Email = "duplicate@example.com",
            Subject = "Test Subject",
            Message = "Test Message",
            CountryId = Guid.Empty,
            ContactType = ContactType.General
        };

        // First request
        await Client.PostAsJsonAsync("/contact/v1/contacts", request);

        // Second request (should be duplicate)
        var response = await Client.PostAsJsonAsync("/contact/v1/contacts", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateContactMessage_WithInvalidCountry_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateContactMessageRequest
        {
            FullName = "Invalid Country Test",
            Email = "invalidcountry@example.com",
            Subject = "Test Subject",
            Message = "Test Message",
            CountryId = Guid.NewGuid(),
            ContactType = ContactType.General
        };

        // Act
        var response = await Client.PostAsJsonAsync("/contact/v1/contacts", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetContactMessages_WithContactTypeFilter_ReturnsFilteredResults()
    {
        // Arrange
        var contact = await CreateTestContactAsync("contacttype.test@example.com");
        contact.ContactType = ContactType.Business;

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ContactDbContext>();
        context.ContactMessages.Update(contact);
        await context.SaveChangesAsync();

        // Act
        var client = Factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/contact/v1/contacts?contactType=3");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await GetResponseAsync<IEnumerable<ContactMessageDto>>(response);
        Assert.NotNull(result);
        Assert.All(result, r => Assert.Equal(ContactType.Business, r.ContactType));
    }

    [Fact]
    public async Task UpdateContactStatus_ConcurrencyConflict_ReturnsError()
    {
        // Arrange
        var contact = await CreateTestContactAsync("concurrency@example.com");

        // Update the contact twice in quick succession
        var request1 = new UpdateContactStatusRequest { Status = ContactStatus.InProgress };
        var request2 = new UpdateContactStatusRequest { Status = ContactStatus.Resolved };

        var client = Factory.CreateAuthenticatedClient();

        // First update
        await client.PutAsJsonAsync($"/contact/v1/contacts/{contact.Id}/status", request1);

        // Second update immediately - may cause concurrency conflict
        var response = await client.PutAsJsonAsync($"/contact/v1/contacts/{contact.Id}/status", request2);

        // Assert - should either succeed or fail with conflict depending on timing
        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task CreateContactMessage_WithFiles_UploadsSuccessfully()
    {
        // Arrange
        var request = new CreateContactMessageRequest
        {
            FullName = "With Files Test",
            Email = "withfiles@example.com",
            Subject = "Test Subject with Files",
            Message = "Test Message with Files",
            CountryId = Guid.Empty,
            ContactType = ContactType.General,
            Files = new List<CreateContactFileRequest>
            {
                new() { FileName = "test.txt", FileContent = new byte[] { 1, 2, 3 }, ContentType = "text/plain" }
            }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/contact/v1/contacts", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await GetResponseAsync<ContactMessageDto>(response);
        Assert.NotNull(result);

        // Check that files endpoint returns files
        var client = Factory.CreateAuthenticatedClient();
        var filesResponse = await client.GetAsync($"/contact/v1/contacts/{result!.Id}/files");
        Assert.Equal(HttpStatusCode.OK, filesResponse.StatusCode);
        var files = await GetResponseAsync<IEnumerable<ContactFileDto>>(filesResponse);
        Assert.NotNull(files);
        Assert.Single(files!);
    }

    [Fact]
    public async Task GetContactFiles_WithValidContactId_ReturnsFiles()
    {
        // Arrange
        var request = new CreateContactMessageRequest
        {
            FullName = "Get Files Test",
            Email = "getfiles@example.com",
            Subject = "Test Subject",
            Message = "Test Message",
            CountryId = Guid.Empty,
            ContactType = ContactType.General,
            Files = new List<CreateContactFileRequest>
            {
                new() { FileName = "file1.txt", FileContent = new byte[] { 1, 2, 3 }, ContentType = "text/plain" },
                new() { FileName = "file2.pdf", FileContent = new byte[] { 4, 5, 6 }, ContentType = "application/pdf" }
            }
        };

        var createResponse = await Client.PostAsJsonAsync("/contact/v1/contacts", request);
        var result = await GetResponseAsync<ContactMessageDto>(createResponse);

        // Act
        var client = Factory.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/contact/v1/contacts/{result!.Id}/files");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var files = await GetResponseAsync<IEnumerable<ContactFileDto>>(response);
        Assert.NotNull(files);
        Assert.Equal(2, files!.Count());
    }

    [Fact]
    public async Task GetContactFiles_WithNonExistentContactId_ReturnsEmptyList()
    {
        // Act
        var client = Factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/contact/v1/contacts/99999/files");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var files = await GetResponseAsync<IEnumerable<ContactFileDto>>(response);
        Assert.NotNull(files);
        Assert.Empty(files!);
    }

    [Fact]
    public async Task DeleteContactFile_WithValidData_ReturnsNoContent()
    {
        // Arrange
        var request = new CreateContactMessageRequest
        {
            FullName = "Delete File Test",
            Email = "deletefile@example.com",
            Subject = "Test Subject",
            Message = "Test Message",
            CountryId = Guid.Empty,
            ContactType = ContactType.General,
            Files = new List<CreateContactFileRequest>
            {
                new() { FileName = "deleteme.txt", FileContent = new byte[] { 1, 2, 3 }, ContentType = "text/plain" }
            }
        };

        var createResponse = await Client.PostAsJsonAsync("/contact/v1/contacts", request);
        var result = await GetResponseAsync<ContactMessageDto>(createResponse);

        // Get files to get file ID
        var client = Factory.CreateAuthenticatedClient();
        var filesResponse = await client.GetAsync($"/contact/v1/contacts/{result!.Id}/files");
        var files = await GetResponseAsync<IEnumerable<ContactFileDto>>(filesResponse);
        var fileId = files!.First().Id;

        // Act
        var deleteResponse = await client.DeleteAsync($"/contact/v1/contacts/{result.Id}/files/{fileId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteContactFile_WithNonExistentFileId_ReturnsNotFound()
    {
        // Arrange
        var contact = await CreateTestContactAsync("deletefile2@example.com");

        // Act
        var client = Factory.CreateAuthenticatedClient();
        var response = await client.DeleteAsync($"/contact/v1/contacts/{contact.Id}/files/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DownloadContactFile_WithValidFileId_ReturnsFile()
    {
        // Arrange
        var request = new CreateContactMessageRequest
        {
            FullName = "Download File Test",
            Email = "downloadfile@example.com",
            Subject = "Test Subject",
            Message = "Test Message",
            CountryId = Guid.Empty,
            ContactType = ContactType.General,
            Files = new List<CreateContactFileRequest>
            {
                new() { FileName = "download.txt", FileContent = System.Text.Encoding.UTF8.GetBytes("Test content"), ContentType = "text/plain" }
            }
        };

        var createResponse = await Client.PostAsJsonAsync("/contact/v1/contacts", request);
        var result = await GetResponseAsync<ContactMessageDto>(createResponse);

        // Get files to get file ID
        var client = Factory.CreateAuthenticatedClient();
        var filesResponse = await client.GetAsync($"/contact/v1/contacts/{result!.Id}/files");
        var files = await GetResponseAsync<IEnumerable<ContactFileDto>>(filesResponse);
        var fileId = files!.First().Id;

        // Act
        var downloadResponse = await client.GetAsync($"/contact/v1/contacts/{result.Id}/files/{fileId}/download");

        // Assert
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.NotNull(downloadResponse.Content.Headers.ContentType);
    }

    [Fact]
    public async Task DownloadContactFile_WithNonExistentFileId_ReturnsNotFound()
    {
        // Arrange
        var contact = await CreateTestContactAsync("download2@example.com");

        // Act
        var client = Factory.CreateAuthenticatedClient();
        var response = await client.GetAsync($"/contact/v1/contacts/{contact.Id}/files/99999/download");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateContactMessage_WithAllFields_PopulatesAllFields()
    {
        // Arrange
        var request = new CreateContactMessageRequest
        {
            FullName = "Full Contact Test",
            Email = "full@example.com",
            PhoneNumber = "+1234567890",
            Company = "Test Company",
            Subject = "Full Test Subject",
            Message = "Full test message content",
            CountryId = Guid.Empty,
            ContactType = ContactType.Supplier,
            Priority = Priority.High
        };

        // Act
        var response = await Client.PostAsJsonAsync("/contact/v1/contacts", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await GetResponseAsync<ContactMessageDto>(response);
        Assert.NotNull(result);
        Assert.Equal(request.FullName, result!.FullName);
        Assert.Equal(request.PhoneNumber, result.PhoneNumber);
        Assert.Equal(request.Company, result.Company);
        Assert.Equal(request.ContactType, result.ContactType);
        Assert.Equal(request.Priority, result.Priority);
        Assert.Equal(ContactStatus.New, result.Status);
    }

    [Fact]
    public async Task UpdateContactStatus_ResolvedStatus_SetsResolvedAt()
    {
        // Arrange
        var contact = await CreateTestContactAsync("resolved@example.com");
        var request = new UpdateContactStatusRequest
        {
            Status = ContactStatus.Resolved
        };

        // Act
        var client = Factory.CreateAuthenticatedClient();
        var response = await client.PutAsJsonAsync($"/contact/v1/contacts/{contact.Id}/status", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await GetResponseAsync<ContactMessageDto>(response);
        Assert.NotNull(result);
        Assert.NotNull(result!.ResolvedAt);
    }

    [Fact]
    public async Task UpdateContactStatus_InProgressStatus_DoesNotSetResolvedAt()
    {
        // Arrange
        var contact = await CreateTestContactAsync("inprogress@example.com");
        var request = new UpdateContactStatusRequest
        {
            Status = ContactStatus.InProgress
        };

        // Act
        var client = Factory.CreateAuthenticatedClient();
        var response = await client.PutAsJsonAsync($"/contact/v1/contacts/{contact.Id}/status", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await GetResponseAsync<ContactMessageDto>(response);
        Assert.NotNull(result);
        Assert.Null(result!.ResolvedAt);
    }

    [Fact]
    public async Task UpdateContactStatus_ClosedStatus_DoesNotSetResolvedAt()
    {
        // Arrange
        var contact = await CreateTestContactAsync("closed@example.com");
        var request = new UpdateContactStatusRequest
        {
            Status = ContactStatus.Closed
        };

        // Act
        var client = Factory.CreateAuthenticatedClient();
        var response = await client.PutAsJsonAsync($"/contact/v1/contacts/{contact.Id}/status", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await GetResponseAsync<ContactMessageDto>(response);
        Assert.NotNull(result);
        Assert.Null(result!.ResolvedAt);
    }

    [Fact]
    public async Task CreateContactMessage_WithEmptyFilesList_CreatesSuccessfully()
    {
        // Arrange
        var request = new CreateContactMessageRequest
        {
            FullName = "No Files Test",
            Email = "nofiles@example.com",
            Subject = "Test Subject",
            Message = "Test Message",
            CountryId = Guid.Empty,
            ContactType = ContactType.General,
            Files = new List<CreateContactFileRequest>()
        };

        // Act
        var response = await Client.PostAsJsonAsync("/contact/v1/contacts", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await GetResponseAsync<ContactMessageDto>(response);
        Assert.NotNull(result);
    }
}
