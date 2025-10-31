namespace Maliev.ContactService.Api.Models;

public class CountryServiceOptions
{
    public required string BaseUrl { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
}
