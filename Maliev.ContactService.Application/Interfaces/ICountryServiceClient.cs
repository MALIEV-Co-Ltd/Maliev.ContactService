namespace Maliev.ContactService.Application.Interfaces;

public interface ICountryServiceClient
{
    Task<bool> ValidateCountryExistsAsync(string countryId);
}
