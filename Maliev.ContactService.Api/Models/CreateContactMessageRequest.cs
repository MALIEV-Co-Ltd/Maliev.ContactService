using System.ComponentModel.DataAnnotations;
using Maliev.ContactService.Data.Models;

namespace Maliev.ContactService.Api.Models;

public class CreateContactMessageRequest
{
    [Required]
    [StringLength(200)]
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
    [StringLength(500)]
    public required string Subject { get; set; }

    [Required]
    [StringLength(10000)]
    public required string Message { get; set; }

    [Required]
    public ContactType ContactType { get; set; } = ContactType.General;

    public Priority Priority { get; set; } = Priority.Medium;

    public List<CreateContactFileRequest> Files { get; set; } = new();
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