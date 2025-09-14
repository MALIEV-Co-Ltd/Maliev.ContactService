namespace Maliev.ContactService.Api.Models;

public class CacheOptions
{
    public const string SectionName = "Cache";

    public int MaxCacheSize { get; set; } = 1000;
    public int DefaultExpirationMinutes { get; set; } = 5;
}