using System.ComponentModel.DataAnnotations;

namespace Maliev.ContactService.Api.Models;

public class UploadServiceOptions
{
    public const string SectionName = "ExternalServices:UploadService";

    [Required]
    public required string BaseUrl { get; set; }

    [Range(1, 300)]
    public int TimeoutInSeconds { get; set; } = 180;
}