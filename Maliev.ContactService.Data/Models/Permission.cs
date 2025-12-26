using System.ComponentModel.DataAnnotations;

namespace Maliev.ContactService.Data.Models;

/// <summary>
/// Represents a granular permission in the system.
/// </summary>
public class Permission
{
    /// <summary>
    /// Gets or sets the unique identifier for the permission.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the permission (e.g., "contact.contacts.create").
    /// </summary>
    [Required]
    [StringLength(100)]
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the description of the permission.
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the category of the permission.
    /// </summary>
    [Required]
    [StringLength(100)]
    public required string Category { get; set; }

    /// <summary>
    /// Gets or sets the collection of roles associated with this permission.
    /// </summary>
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
