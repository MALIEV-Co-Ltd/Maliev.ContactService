using Maliev.ContactService.Data.Models;
using System.ComponentModel.DataAnnotations;

namespace Maliev.ContactService.Api.Models;
/// <summary>
/// Request model for createcontactmessage
/// </summary>

public class CreateContactMessageRequest : IValidatableObject
{
    /// <summary>
    /// Gets or sets the full name of the person submitting the contact message.
    /// </summary>
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public required string FullName { get; set; }

    /// <summary>
    /// Gets or sets the email address of the person submitting the contact message.
    /// </summary>
    [Required]
    [EmailAddress]
    [StringLength(254)]
    public required string Email { get; set; }

    /// <summary>
    /// Gets or sets the phone number of the person submitting the contact message.
    /// </summary>
    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the company name of the person submitting the contact message.
    /// </summary>
    [StringLength(200)]
    public string? Company { get; set; }

    /// <summary>
    /// Gets or sets the subject of the contact message.
    /// </summary>
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public required string Subject { get; set; }

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    [Required]
    [StringLength(10000, MinimumLength = 1)]
    public required string Message { get; set; }

    /// <summary>
    /// Gets or sets the country identifier associated with the contact message.
    /// </summary>
    [Required]
    public Guid CountryId { get; set; }

    /// <summary>
    /// Gets or sets the type of contact inquiry.
    /// </summary>
    [Required]
    public ContactType ContactType { get; set; } = ContactType.General;

    /// <summary>
    /// Gets or sets the priority level of the contact message.
    /// </summary>
    public Priority Priority { get; set; } = Priority.Medium;

    /// <summary>
    /// Gets or sets the list of files to attach to the contact message.
    /// </summary>
    public List<CreateContactFileRequest> Files { get; set; } = new();

    /// <summary>
    /// Validates the contact message request according to business rules.
    /// </summary>
    /// <param name="validationContext">The validation context.</param>
    /// <returns>A collection of validation results.</returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // FR-013: Reject invalid or excluded contact types
        if (!Enum.IsDefined(typeof(ContactType), ContactType))
        {
            yield return new ValidationResult(
                "The selected contact type is invalid or not accepted through this service.",
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
/// <summary>
/// Request model for createcontactfile
/// </summary>

public class CreateContactFileRequest
{
    /// <summary>
    /// Gets or sets the name of the file.
    /// </summary>
    [Required]
    [StringLength(255)]
    public required string FileName { get; set; }

    /// <summary>
    /// Gets or sets the binary content of the file.
    /// </summary>
    [Required]
    public required byte[] FileContent { get; set; }

    /// <summary>
    /// Gets or sets the MIME content type of the file.
    /// </summary>
    [StringLength(100)]
    public string? ContentType { get; set; }
}
