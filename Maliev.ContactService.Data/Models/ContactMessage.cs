using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.ContactService.Data.Models;

public class ContactMessage : IAuditable
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public required string FullName { get; set; }

    [Required]
    [StringLength(254)]
    [EmailAddress]
    public required string Email { get; set; }

    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    [StringLength(200)]
    public string? Company { get; set; }

    [Required]
    [StringLength(500)]
    public required string Subject { get; set; }

    [Required]
    public required string Message { get; set; }

    [Required]
    public ContactType ContactType { get; set; } = ContactType.General;

    [Required]
    public Priority Priority { get; set; } = Priority.Medium;

    [Required]
    public ContactStatus Status { get; set; } = ContactStatus.New;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAt { get; set; }

    // Navigation property
    public virtual ICollection<ContactFile> Files { get; set; } = new List<ContactFile>();
}

public enum ContactType
{
    General = 0,
    Supplier = 1,
    Quotation = 2,
    Business = 3
}

public enum Priority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Urgent = 3
}

public enum ContactStatus
{
    New = 0,
    InProgress = 1,
    Resolved = 2,
    Closed = 3
}