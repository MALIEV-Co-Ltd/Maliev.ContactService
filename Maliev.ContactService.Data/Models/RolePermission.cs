using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.ContactService.Data.Models;

/// <summary>
/// Join table for the many-to-many relationship between roles and permissions.
/// </summary>
public class RolePermission
{
    /// <summary>
    /// Gets or sets the role identifier.
    /// </summary>
    [Required]
    public Guid RoleId { get; set; }

    /// <summary>
    /// Gets or sets the role.
    /// </summary>
    [ForeignKey(nameof(RoleId))]
    public virtual Role Role { get; set; } = null!;

    /// <summary>
    /// Gets or sets the permission identifier.
    /// </summary>
    [Required]
    public Guid PermissionId { get; set; }

    /// <summary>
    /// Gets or sets the permission.
    /// </summary>
    [ForeignKey(nameof(PermissionId))]
    public virtual Permission Permission { get; set; } = null!;
}
