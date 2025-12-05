namespace Maliev.ContactService.Api.Models;

/// <summary>
/// Configuration options for the external Country Service.
/// </summary>
public class CountryServiceOptions
{
    /// <summary>
    /// Gets the configuration section name for country service options.
    /// </summary>
    public const string SectionName = "ExternalServices:CountryService";

    /// <summary>
    /// The base URL for the Country Service API.
    /// </summary>
    public required string BaseUrl { get; set; }
    /// <summary>
    /// The timeout in seconds for Country Service API requests.
    /// </summary>
    public int TimeoutInSeconds { get; set; } = 180;
}
