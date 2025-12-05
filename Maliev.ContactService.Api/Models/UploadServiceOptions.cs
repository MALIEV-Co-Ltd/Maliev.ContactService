using System.ComponentModel.DataAnnotations;

namespace Maliev.ContactService.Api.Models;
/// <summary>
/// Configuration options for UploadService
/// </summary>

public class UploadServiceOptions
{
    /// <summary>
    /// Gets the configuration section name for upload service options.
    /// </summary>
    public const string SectionName = "ExternalServices:UploadService";

    /// <summary>
    /// Gets or sets the base URL for the Upload Service API.
    /// </summary>
    [Required]
    public required string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the timeout in seconds for Upload Service API requests.
    /// </summary>
    [Range(1, 300)]
    public int TimeoutInSeconds { get; set; } = 180;
}
