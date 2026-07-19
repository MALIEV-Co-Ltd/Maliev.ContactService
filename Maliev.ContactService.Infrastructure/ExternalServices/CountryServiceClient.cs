using System.Net.Http.Json;
using Maliev.ContactService.Application.Exceptions;
using Maliev.ContactService.Application.Interfaces;

namespace Maliev.ContactService.Infrastructure.ExternalServices;

public class CountryServiceClient : ICountryServiceClient
{
    private readonly HttpClient _httpClient;

    public CountryServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> ValidateCountryExistsAsync(string countryId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/country/v1/countries/{countryId}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<CountryValidationResponse>();
                return result?.IsActive ?? false;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }

            throw new CountryServiceException(
                "Unable to validate country information. Please try again in a few moments.");
        }
        catch (CountryServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CountryServiceException(
                "Unable to validate country information. Please try again in a few moments.", ex);
        }
    }

    private record CountryValidationResponse(bool IsActive);
}
