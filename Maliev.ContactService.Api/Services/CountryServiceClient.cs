using Maliev.ContactService.Api.Exceptions;
using Maliev.ContactService.Api.Models;
using Microsoft.Extensions.Options;
using System.Net;

namespace Maliev.ContactService.Api.Services;

public class CountryServiceClient : ICountryServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CountryServiceClient> _logger;
    private readonly CountryServiceOptions _options;

    public CountryServiceClient(
        HttpClient httpClient,
        ILogger<CountryServiceClient> logger,
        IOptions<CountryServiceOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        // Configure base address and timeout
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public async Task<bool> ValidateCountryExistsAsync(int countryId, CancellationToken cancellationToken = default)
    {
        // Note: Polly retry and circuit breaker policies should be configured in Program.cs when registering
        // the HttpClient. The current implementation uses basic HttpClient timeout (10 seconds) configured
        // via CountryServiceOptions. For production resilience, consider adding:
        // - Retry policy: 3 retries with exponential backoff (2^attempt seconds)
        // - Circuit breaker: Opens after 5 consecutive failures, stays open for 30 seconds
        // This protects the Contact Service from cascading failures if Country Service is unavailable.
        try
        {
            _logger.LogInformation("Validating country ID {CountryId} via Country Service", countryId);

            var response = await _httpClient.GetAsync($"/countries/v1/{countryId}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Country ID {CountryId} not found in Country Service", countryId);
                return false;
            }

            response.EnsureSuccessStatusCode();

            var country = await response.Content.ReadFromJsonAsync<CountryDto>(cancellationToken: cancellationToken);

            if (country == null)
            {
                _logger.LogWarning("Country ID {CountryId} returned null response", countryId);
                return false;
            }

            if (!country.IsActive)
            {
                _logger.LogWarning("Country ID {CountryId} is not active", countryId);
                return false;
            }

            _logger.LogInformation("Country ID {CountryId} validated successfully", countryId);
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request to Country Service failed for country ID {CountryId}", countryId);
            throw new CountryServiceException("Unable to validate country information. Please try again in a few moments.", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Request to Country Service timed out for country ID {CountryId}", countryId);
            throw new CountryServiceException("Unable to validate country information. Please try again in a few moments.", ex);
        }
    }
}
