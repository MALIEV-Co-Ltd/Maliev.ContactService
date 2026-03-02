using Maliev.ContactService.Application.Interfaces;

namespace Maliev.ContactService.Tests.Services;

public class MockCountryServiceClient : ICountryServiceClient
{
    public Task<bool> ValidateCountryExistsAsync(string countryId)
    {
        return Task.FromResult(true);
    }
}
