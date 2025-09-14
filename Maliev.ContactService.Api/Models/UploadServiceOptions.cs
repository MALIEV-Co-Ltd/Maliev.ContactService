using System.ComponentModel.DataAnnotations;

namespace Maliev.ContactService.Api.Models;

public class UploadServiceOptions
{
    public const string SectionName = "UploadService";

    [Required]
    public required string BaseUrl { get; set; }

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}