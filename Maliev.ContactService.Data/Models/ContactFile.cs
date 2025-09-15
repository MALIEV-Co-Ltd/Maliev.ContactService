using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.ContactService.Data.Models;

public class ContactFile : IAuditable
{
    [Key]
    public int Id { get; set; }

    [Required]
    [ForeignKey(nameof(ContactMessage))]
    public int ContactMessageId { get; set; }

    [Required]
    [StringLength(255)]
    public required string FileName { get; set; }

    [Required]
    [StringLength(500)]
    public required string ObjectName { get; set; }

    public long? FileSize { get; set; }

    [StringLength(100)]
    public string? ContentType { get; set; }

    [StringLength(100)]
    public string? UploadServiceFileId { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Required]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation property
    public virtual ContactMessage ContactMessage { get; set; } = null!;
}