namespace Maliev.ContactService.Api.Models;

public class CountryServiceOptions
{
    public const string SectionName = "ExternalServices:CountryService";

    public required string BaseUrl { get; set; }
    public int TimeoutInSeconds { get; set; } = 180;
}
