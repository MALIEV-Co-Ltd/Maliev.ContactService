using Maliev.ContactService.Application.Interfaces;

namespace Maliev.ContactService.Tests.Services;

public class MockCountryServiceClient : ICountryServiceClient
{
    public static readonly HashSet<string> ValidCountryIds = new() { Guid.Empty.ToString(), "00000000-0000-0000-0000-000000000000" };

    public Task<bool> ValidateCountryExistsAsync(string countryId)
    {
        return Task.FromResult(ValidCountryIds.Contains(countryId));
    }
}
