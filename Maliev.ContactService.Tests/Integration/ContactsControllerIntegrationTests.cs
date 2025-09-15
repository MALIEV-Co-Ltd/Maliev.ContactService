using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
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
        // Act
        var response = await _client.GetAsync("/v1/contacts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contacts = await response.Content.ReadFromJsonAsync<IEnumerable<ContactMessageDto>>();
        contacts.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateContactMessage_Should_Return_Created()
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

        // Act
        var response = await _client.PostAsJsonAsync("/v1/contacts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var contact = await response.Content.ReadFromJsonAsync<ContactMessageDto>();
        contact.Should().NotBeNull();
    }
}