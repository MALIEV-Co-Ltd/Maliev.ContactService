using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.ContactService.Data.Models;

/// <summary>
/// Represents a file attachment associated with a contact message.
/// </summary>
public class ContactFile : IAuditable
{
    /// <summary>
    /// Gets or sets the unique identifier for the contact file.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the associated contact message.
    /// </summary>
    [Required]
    [ForeignKey(nameof(ContactMessage))]
    public int ContactMessageId { get; set; }

    /// <summary>
    /// Gets or sets the original file name.
    /// </summary>
    [Required]
    [StringLength(255)]
    public required string FileName { get; set; }

    /// <summary>
    /// Gets or sets the object name in the storage system.
    /// </summary>
    [Required]
    [StringLength(500)]
    public required string ObjectName { get; set; }

    /// <summary>
    /// Gets or sets the size of the file in bytes.
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// Gets or sets the MIME content type of the file.
    /// </summary>
    [StringLength(100)]
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets the file identifier from the upload service.
    /// </summary>
    [StringLength(100)]
    public string? UploadServiceFileId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the file was created.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when the file was last updated.
    /// </summary>
    [Required]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the navigation property to the associated contact message.
    /// </summary>
    public virtual ContactMessage ContactMessage { get; set; } = null!;
}
