using System.Net;
using System.Net.Http.Json;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Data.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Maliev.ContactService.Tests.Integration;

/// <summary>
/// Integration tests for ContactsController using WebApplicationFactory
/// These tests demonstrate the approach suggested in GitHub issue #34
/// </summary>
[Trait("Category", "Integration")]
public class ContactsControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ContactsControllerIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetContactMessages_Should_Return_Success_With_Proper_Auth()
    {
        // Act - Use correct route with service prefix: /contacts/v{version}/contacts
        var response = await _client.GetAsync("/contacts/v1/contacts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contacts = await response.Content.ReadFromJsonAsync<IEnumerable<ContactMessageDto>>();
        Assert.NotNull(contacts);
    }

    [Fact]
    public async Task CreateContactMessage_Should_Return_Created()
    {
        // Arrange
        var request = new CreateContactMessageRequest
        {
            FullName = "John Doe",
            Email = $"john.integration.{Guid.NewGuid():N}@example.com",
            Subject = "Test Subject",
            Message = "Test Message",
            CountryId = 1,
            ContactType = ContactType.General
        };

        // Act - Use correct route with service prefix: /contacts/v{version}/contacts
        var response = await _client.PostAsJsonAsync("/contacts/v1/contacts", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var contact = await response.Content.ReadFromJsonAsync<ContactMessageDto>();
        Assert.NotNull(contact);
    }
}