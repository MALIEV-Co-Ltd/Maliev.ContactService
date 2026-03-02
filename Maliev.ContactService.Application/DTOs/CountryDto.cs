namespace Maliev.ContactService.Application.DTOs;

/// <summary>
/// Data Transfer Object for country information.
/// </summary>
public class CountryDto
{
    /// <summary>
    /// The unique identifier of the country.
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// The name of the country.
    /// </summary>
    public required string Name { get; set; }
    /// <summary>
    /// The ISO 3166-1 alpha-2 country code (e.g., "US", "GB", "TH").
    /// </summary>
    public required string Iso2 { get; set; }
    /// <summary>
    /// Indicates whether the country is active.
    /// </summary>
    public bool IsActive { get; set; }
}
