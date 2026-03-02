using System.Net.Http.Json;
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
            var response = await _httpClient.GetAsync($"/v1/countries/{countryId}/validate");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ValidationResult>();
                return result?.IsValid ?? false;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private record ValidationResult(bool IsValid);
}
