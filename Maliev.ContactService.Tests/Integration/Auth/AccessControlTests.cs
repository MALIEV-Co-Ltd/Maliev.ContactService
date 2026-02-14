using System.Net;
using System.Net.Http.Json;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Api.Services.Auth;
using Maliev.ContactService.Data.Models;

namespace Maliev.ContactService.Tests.Integration.Auth;

[Trait("Category", "Integration")]
public class AccessControlTests : IClassFixture<CustomWebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private HttpClient _adminClient = null!;
    private HttpClient _userClient = null!;
    private HttpClient _viewerClient = null!;

    public AccessControlTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();

        var adminPerms = ContactPredefinedRoles.GetPermissionsForRole(ContactPredefinedRoles.Admin).ToArray();
        var userPerms = ContactPredefinedRoles.GetPermissionsForRole(ContactPredefinedRoles.User).ToArray();
        var viewerPerms = ContactPredefinedRoles.GetPermissionsForRole(ContactPredefinedRoles.Viewer).ToArray();

        _adminClient = _factory.CreateAuthenticatedClient("admin-user", new[] { ContactPredefinedRoles.Admin }, adminPerms);
        _userClient = _factory.CreateAuthenticatedClient("normal-user", new[] { ContactPredefinedRoles.User }, userPerms);
        _viewerClient = _factory.CreateAuthenticatedClient("viewer-user", new[] { ContactPredefinedRoles.Viewer }, viewerPerms);
    }

    public async Task DisposeAsync()
    {
        _adminClient?.Dispose();
        _userClient?.Dispose();
        _viewerClient?.Dispose();
        await _factory.ResetDatabaseAsync();
    }

    [Fact]
    public async Task Admin_Should_Have_Full_Access()
    {
        // 1. Can Read
        var getResponse = await _adminClient.GetAsync("/contact/v1/contacts");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        // 2. Can Create (via public endpoint, but authenticated)
        var createResponse = await _adminClient.PostAsJsonAsync("/contact/v1/contacts", CreateRequest("Admin Inquiry"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var contact = await createResponse.Content.ReadFromJsonAsync<ContactMessageDto>();

        // 3. Can Update Status
        var updateResponse = await _adminClient.PutAsJsonAsync($"/contact/v1/contacts/{contact!.Id}/status", new UpdateContactStatusRequest { Status = ContactStatus.InProgress });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        // 4. Can Delete
        var deleteResponse = await _adminClient.DeleteAsync($"/contact/v1/contacts/{contact.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task User_Should_Be_Restricted()
    {
        // 1. Can Create
        var createResponse = await _userClient.PostAsJsonAsync("/contact/v1/contacts", CreateRequest("User Inquiry"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var contact = await createResponse.Content.ReadFromJsonAsync<ContactMessageDto>();

        // 2. CANNOT Delete
        var deleteResponse = await _userClient.DeleteAsync($"/contact/v1/contacts/{contact!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);

        // 3. CAN Update Status (User role HAS contacts.update)
        var updateResponse = await _userClient.PutAsJsonAsync($"/contact/v1/contacts/{contact.Id}/status", new UpdateContactStatusRequest { Status = ContactStatus.InProgress });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        // 4. CANNOT Merge (User role does NOT have contacts.merge)
        var mergeResponse = await _userClient.PostAsync($"/contact/v1/contacts/{contact.Id}/merge", null);
        Assert.Equal(HttpStatusCode.Forbidden, mergeResponse.StatusCode);
    }

    [Fact]
    public async Task Viewer_Should_Be_ReadOnly()
    {
        // Arrange - Admin creates a contact
        var createResponse = await _adminClient.PostAsJsonAsync("/contact/v1/contacts", CreateRequest("To View"));
        var contact = await createResponse.Content.ReadFromJsonAsync<ContactMessageDto>();

        // 1. Can Read
        var getResponse = await _viewerClient.GetAsync($"/contact/v1/contacts/{contact!.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        // 2. CANNOT Update
        var updateResponse = await _viewerClient.PutAsJsonAsync($"/contact/v1/contacts/{contact.Id}/status", new UpdateContactStatusRequest { Status = ContactStatus.InProgress });
        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);

        // 3. CANNOT Delete
        var deleteResponse = await _viewerClient.DeleteAsync($"/contact/v1/contacts/{contact.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }

    private static CreateContactMessageRequest CreateRequest(string subject) => new()
    {
        FullName = "Test Person",
        Email = $"test.{Guid.NewGuid():N}@example.com",
        Subject = subject,
        Message = "Test message content",
        CountryId = Guid.Empty,
        ContactType = ContactType.General
    };
}
