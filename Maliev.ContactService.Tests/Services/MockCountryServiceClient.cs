using Maliev.ContactService.Api.Services;

namespace Maliev.ContactService.Tests.Services;

public class MockCountryServiceClient : ICountryServiceClient
{
    public Task<bool> ValidateCountryExistsAsync(Guid countryId, CancellationToken cancellationToken = default)
    {
        // Mock always returns true for any country ID
        return Task.FromResult(true);
    }

    public Task<bool> ValidateCountryExistsAsync(int countryId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
