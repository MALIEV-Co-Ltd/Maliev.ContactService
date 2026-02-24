using System.ComponentModel.DataAnnotations;

namespace Maliev.ContactService.Data.Models;

/// <summary>
/// Represents an audit log entry for authorization decisions.
/// </summary>
public class AuditLog
{
    /// <summary>
    /// Gets or sets the unique identifier for the audit log entry.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the action occurred.
    /// </summary>
    [Required]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the user identifier from the JWT subject.
    /// </summary>
    [Required]
    [StringLength(100)]
    public required string UserId { get; set; }

    /// <summary>
    /// Gets or sets the requested action or permission.
    /// </summary>
    [Required]
    [StringLength(100)]
    public required string Action { get; set; }

    /// <summary>
    /// Gets or sets the resource being accessed.
    /// </summary>
    [Required]
    [StringLength(200)]
    public required string Resource { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the authorization was successful.
    /// </summary>
    [Required]
    public bool Result { get; set; }

    /// <summary>
    /// Gets or sets the reason for the decision (especially on failure).
    /// </summary>
    [StringLength(500)]
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the IP address of the client.
    /// </summary>
    [StringLength(50)]
    public string? ClientIp { get; set; }
}
