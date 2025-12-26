namespace Maliev.ContactService.Api.Models;

/// <summary>
/// Configuration options for caching.
/// </summary>
public class CacheOptions
{
    /// <summary>
    /// Gets the configuration section name for cache options.
    /// </summary>
    public const string SectionName = "Cache";

    /// <summary>
    /// Gets or sets the maximum number of items the cache can hold.
    /// </summary>
    public int MaxCacheSize { get; set; } = 1000;
    /// <summary>
    /// Gets or sets the default expiration time for cache entries in minutes.
    /// </summary>
    public int DefaultExpirationMinutes { get; set; } = 5;
}
