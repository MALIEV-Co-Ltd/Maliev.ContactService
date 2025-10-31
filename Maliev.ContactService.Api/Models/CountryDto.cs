namespace Maliev.ContactService.Api.Models;

public class CountryDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Code { get; set; }
    public bool IsActive { get; set; }
}
