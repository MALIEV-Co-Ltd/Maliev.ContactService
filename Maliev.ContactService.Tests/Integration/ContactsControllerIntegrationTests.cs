using System.Net;
using System.Net.Http.Json;
using Maliev.ContactService.Application.DTOs;
using Maliev.ContactService.Domain.Entities;
using Maliev.ContactService.Tests.Integration.Infrastructure;
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
    }

    [Fact]
    public async Task GetContactMessage_ExistingId_ReturnsOk()
    {
        // Arrange
        var contact = await CreateTestContactAsync();

        // Act
        var response = await Client.GetAsync($"/contact/v1/contacts/{contact.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await GetResponseAsync<ContactMessageDto>(response);
        Assert.NotNull(result);
        Assert.Equal(contact.Id, result!.Id);
    }
}
