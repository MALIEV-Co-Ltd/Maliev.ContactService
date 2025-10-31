using Maliev.ContactService.Api.Exceptions;

namespace Maliev.ContactService.Api.Services;

public interface ICountryServiceClient
{
    /// <summary>
    /// Validates that a country exists and is active in the Country Service.
    /// </summary>
    /// <param name="countryId">The country ID to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if country exists and is active, false otherwise</returns>
    /// <exception cref="CountryServiceException">Thrown when Country Service is unavailable</exception>
    Task<bool> ValidateCountryExistsAsync(int countryId, CancellationToken cancellationToken = default);
}
