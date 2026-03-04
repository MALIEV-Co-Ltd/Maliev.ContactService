using System.Net.Http.Json;
using System.Text.Json;
using Maliev.ContactService.Application.DTOs;
using Maliev.ContactService.Domain.Entities;
using Maliev.ContactService.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Maliev.ContactService.Tests.Integration.Infrastructure;

public abstract class BaseIntegrationTest
{
    protected readonly IntegrationTestWebAppFactory Factory;
    protected readonly HttpClient Client;
    protected readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    protected BaseIntegrationTest(IntegrationTestWebAppFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    protected async Task<T?> GetResponseAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, JsonOptions);
    }

    protected async Task<ContactMessage> CreateTestContactAsync(string email = "test@example.com")
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ContactDbContext>();

        var contact = new ContactMessage
        {
            FullName = "Test User",
            Email = email,
            Subject = "Test Subject",
            Message = "Test Message",
            CountryId = Guid.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Status = ContactStatus.New,
            Priority = Priority.Medium,
            ContactType = ContactType.General,
            RowVersion = 0
        };

        context.ContactMessages.Add(contact);
        await context.SaveChangesAsync();

        return contact;
    }
}
