using System.Net;
using System.Net.Http.Json;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Api.Services.Auth;
using Maliev.ContactService.Data.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Maliev.ContactService.Tests.Integration;

/// <summary>
/// Integration tests for ContactsController using WebApplicationFactory.
/// These tests use a real PostgreSQL database via Testcontainers.
/// </summary>
[Trait("Category", "Integration")]
public class ContactsControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private HttpClient _client = null!;

    public ContactsControllerIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        // The factory and its database container are initialized here
        await _factory.InitializeAsync();
        var permissions = ContactPredefinedRoles.GetPermissionsForRole(ContactPredefinedRoles.Admin).ToArray();
        _client = _factory.CreateAuthenticatedClient("test-user", new[] { ContactPredefinedRoles.Admin }, permissions);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task GetContactMessages_Should_Return_Success_With_Proper_Auth()
    {
        // Act - Use correct route with service prefix: /contacts/v{version}/contacts
        var response = await _client.GetAsync("/contact/v1/contacts");

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
        var response = await _client.PostAsJsonAsync("/contact/v1/contacts", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var contact = await response.Content.ReadFromJsonAsync<ContactMessageDto>();
        Assert.NotNull(contact);
    }

    [Fact]
    public async Task CreateContactMessage_Should_Return_BadRequest_When_ModelInvalid()
    {
        // Arrange
        var request = new CreateContactMessageRequest
        {
            FullName = "", // Invalid
            Email = "invalid-email",
            Subject = "Test Subject",
            Message = "Test Message",
            CountryId = 1,
            ContactType = ContactType.General
        };

        // Act
        var response = await _client.PostAsJsonAsync("/contact/v1/contacts", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetContactMessage_Should_Return_NotFound_When_NotExists()
    {
        // Act - Use an integer ID that doesn't exist (e.g., 999999)
        var response = await _client.GetAsync("/contact/v1/contacts/999999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
