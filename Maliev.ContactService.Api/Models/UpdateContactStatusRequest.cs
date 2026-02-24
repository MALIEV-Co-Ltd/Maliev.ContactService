using Maliev.ContactService.Data.Models;
using System.ComponentModel.DataAnnotations;

namespace Maliev.ContactService.Api.Models;
/// <summary>
/// Request model for updatecontactstatus
/// </summary>

public class UpdateContactStatusRequest
{
    /// <summary>
    /// Gets or sets the new status for the contact message.
    /// </summary>
    [Required]
    public ContactStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the new priority for the contact message (optional).
    /// </summary>
    public Priority? Priority { get; set; }
}
