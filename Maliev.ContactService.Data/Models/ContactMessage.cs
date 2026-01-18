using System.ComponentModel.DataAnnotations;

namespace Maliev.ContactService.Data.Models;

/// <summary>
/// Represents a contact message submitted through the contact form.
/// </summary>
public class ContactMessage : IAuditable
{
    /// <summary>
    /// Gets or sets the unique identifier for the contact message.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the full name of the person submitting the contact message.
    /// </summary>
    [Required]
    [StringLength(200)]
    public required string FullName { get; set; }

    /// <summary>
    /// Gets or sets the email address of the person submitting the contact message.
    /// </summary>
    [Required]
    [StringLength(254)]
    [EmailAddress]
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
    [StringLength(500)]
    public required string Subject { get; set; }

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    [Required]
    public required string Message { get; set; }

    /// <summary>
    /// Gets or sets the country identifier associated with the contact message.
    /// </summary>
    [Required]
    public int CountryId { get; set; }

    /// <summary>
    /// Gets or sets the type of contact inquiry.
    /// </summary>
    [Required]
    public ContactType ContactType { get; set; } = ContactType.General;

    /// <summary>
    /// Gets or sets the priority level of the contact message.
    /// </summary>
    [Required]
    public Priority Priority { get; set; } = Priority.Medium;

    /// <summary>
    /// Gets or sets the current status of the contact message.
    /// </summary>
    [Required]
    public ContactStatus Status { get; set; } = ContactStatus.New;

    /// <summary>
    /// Gets or sets the timestamp when the contact message was created.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when the contact message was last updated.
    /// </summary>
    [Required]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when the contact message was resolved.
    /// </summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>
    /// Gets or sets the row version for concurrency control.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets or sets the collection of files attached to this contact message.
    /// </summary>
    public virtual ICollection<ContactFile> Files { get; set; } = new List<ContactFile>();
}

/// <summary>
/// Specifies the type of contact inquiry.
/// </summary>
public enum ContactType
{
    /// <summary>
    /// General inquiry or question.
    /// </summary>
    General = 0,

    /// <summary>
    /// Supplier-related inquiry.
    /// </summary>
    Supplier = 1,

    /// <summary>
    /// Business partnership or collaboration inquiry.
    /// </summary>
    Business = 3
}

/// <summary>
/// Specifies the priority level of a contact message.
/// </summary>
public enum Priority
{
    /// <summary>
    /// Low priority - can be addressed when convenient.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium priority - standard response time.
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High priority - requires prompt attention.
    /// </summary>
    High = 2,

    /// <summary>
    /// Urgent - requires immediate attention.
    /// </summary>
    Urgent = 3
}

/// <summary>
/// Specifies the processing status of a contact message.
/// </summary>
public enum ContactStatus
{
    /// <summary>
    /// New message that hasn't been reviewed yet.
    /// </summary>
    New = 0,

    /// <summary>
    /// Message is currently being processed.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Message has been resolved.
    /// </summary>
    Resolved = 2,

    /// <summary>
    /// Message has been closed and archived.
    /// </summary>
    Closed = 3
}
