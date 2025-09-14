using System.ComponentModel.DataAnnotations;
using Maliev.ContactService.Data.Models;

namespace Maliev.ContactService.Api.Models;

public class UpdateContactStatusRequest
{
    [Required]
    public ContactStatus Status { get; set; }

    public Priority? Priority { get; set; }
}