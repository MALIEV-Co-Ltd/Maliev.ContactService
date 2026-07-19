using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.ContactService.Domain.Constants;

namespace Maliev.ContactService.Api.Services;

/// <summary>
/// Background service that registers permissions and roles with the IAM service on startup.
/// Uses the standard IAMRegistrationService base class.
/// </summary>
public class ContactIAMRegistrationService : IAMRegistrationService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactIAMRegistrationService"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logger">The logger.</param>
    public ContactIAMRegistrationService(
        IConfiguration configuration,
        ILogger<ContactIAMRegistrationService> logger)
        : base(configuration, logger, "contact")
    {
    }

    /// <inheritdoc/>
    protected override IEnumerable<PermissionRegistration> GetPermissions()
    {
        return ContactPermissions.AllWithDescriptions.Select(p => new PermissionRegistration
        {
            PermissionId = p.Key,
            Description = p.Value
        });
    }

    /// <inheritdoc/>
    protected override IEnumerable<RoleRegistration> GetPredefinedRoles()
    {
        return ContactPredefinedRoles.All.Select(r => new RoleRegistration
        {
            RoleId = r.RoleId,
            Description = r.Description,
            PermissionIds = r.Permissions.ToList(),
            IsCustom = false
        });
    }
}
