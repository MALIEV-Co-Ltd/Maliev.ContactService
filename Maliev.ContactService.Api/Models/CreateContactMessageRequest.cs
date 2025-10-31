using System.ComponentModel.DataAnnotations;
using Maliev.ContactService.Data.Models;

namespace Maliev.ContactService.Api.Models;

public class CreateContactMessageRequest : IValidatableObject
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public required string FullName { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(254)]
    public required string Email { get; set; }

    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    [StringLength(200)]
    public string? Company { get; set; }

    [Required]
    [StringLength(500, MinimumLength = 1)]
    public required string Subject { get; set; }

    [Required]
    [StringLength(10000, MinimumLength = 1)]
    public required string Message { get; set; }

    [Required]
    [Range(1, 999)]
    public int CountryId { get; set; }

    [Required]
    public ContactType ContactType { get; set; } = ContactType.General;

    public Priority Priority { get; set; } = Priority.Medium;

    public List<CreateContactFileRequest> Files { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // FR-013: Reject Quotation contact type
        if (ContactType == (ContactType)2) // Quotation enum value (removed from enum but still possible in requests)
        {
            yield return new ValidationResult(
                "Quotation inquiries are not accepted through this service. Please use the Quotation Service.",
                new[] { nameof(ContactType) });
        }

        // FR-010: Maximum 10 files per inquiry
        if (Files.Count > 10)
        {
            yield return new ValidationResult(
                "Maximum 10 files allowed per inquiry.",
                new[] { nameof(Files) });
        }

        // T037: File size validation - 25MB per file
        const int maxFileSizeBytes = 25 * 1024 * 1024; // 25MB in bytes
        for (int i = 0; i < Files.Count; i++)
        {
            if (Files[i].FileContent.Length > maxFileSizeBytes)
            {
                yield return new ValidationResult(
                    $"File '{Files[i].FileName}' exceeds the maximum size of 25MB. File size: {Files[i].FileContent.Length / (1024.0 * 1024.0):F2}MB",
                    new[] { $"{nameof(Files)}[{i}]" });
            }
        }
    }
}

public class CreateContactFileRequest
{
    [Required]
    [StringLength(255)]
    public required string FileName { get; set; }

    [Required]
    public required byte[] FileContent { get; set; }

    [StringLength(100)]
    public string? ContentType { get; set; }
}