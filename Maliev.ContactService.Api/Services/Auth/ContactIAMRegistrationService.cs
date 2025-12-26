using Maliev.Aspire.ServiceDefaults.IAM;

namespace Maliev.ContactService.Api.Services.Auth;

/// <summary>
/// Background service that registers permissions and roles with the IAM service on startup.
/// Uses the standard IAMRegistrationService base class.
/// </summary>
public class ContactIAMRegistrationService : IAMRegistrationService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactIAMRegistrationService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    public ContactIAMRegistrationService(
        IHttpClientFactory httpClientFactory,
        ILogger<ContactIAMRegistrationService> logger)
        : base(httpClientFactory, logger, "contact")
    {
    }

    /// <inheritdoc/>
    protected override IEnumerable<PermissionRegistration> GetPermissions()
    {
        return ContactPermissions.GetAll().Select(p => new PermissionRegistration
        {
            PermissionId = p,
            Description = GetPermissionDescription(p)
        });
    }

    /// <inheritdoc/>
    protected override IEnumerable<RoleRegistration> GetPredefinedRoles()
    {
        return new[]
        {
            new RoleRegistration
            {
                RoleId = "roles.contact.admin",
                Description = "Full access to all contact operations",
                PermissionIds = ContactPermissions.GetAll().ToList(),
                IsCustom = false
            },
            new RoleRegistration
            {
                RoleId = "roles.contact.manager",
                Description = "Most contact access (cannot merge)",
                PermissionIds = ContactPermissions.GetAll()
                    .Where(p => p != ContactPermissions.Contacts.Merge)
                    .ToList(),
                IsCustom = false
            },
            new RoleRegistration
            {
                RoleId = "roles.contact.user",
                Description = "Standard contact access",
                PermissionIds = new List<string>
                {
                    ContactPermissions.Contacts.Create,
                    ContactPermissions.Contacts.Read,
                    ContactPermissions.Contacts.Update,
                    ContactPermissions.Communications.Log,
                    ContactPermissions.Communications.Read
                },
                IsCustom = false
            },
            new RoleRegistration
            {
                RoleId = "roles.contact.viewer",
                Description = "Read-only access to contacts",
                PermissionIds = new List<string>
                {
                    ContactPermissions.Contacts.Read,
                    ContactPermissions.Communications.Read,
                    ContactPermissions.Groups.Read
                },
                IsCustom = false
            }
        };
    }

    private static string GetPermissionDescription(string permission)
    {
        return permission switch
        {
            ContactPermissions.Contacts.Create => "Create contacts",
            ContactPermissions.Contacts.Read => "Read contacts",
            ContactPermissions.Contacts.Update => "Update contacts",
            ContactPermissions.Contacts.Delete => "Delete contacts",
            ContactPermissions.Contacts.Merge => "Merge contacts",
            ContactPermissions.Contacts.Export => "Export contacts",
            ContactPermissions.Communications.Log => "Log communications",
            ContactPermissions.Communications.Read => "Read communications",
            ContactPermissions.Communications.Delete => "Delete communications",
            ContactPermissions.Groups.Create => "Create groups",
            ContactPermissions.Groups.Read => "Read groups",
            ContactPermissions.Groups.Update => "Update groups",
            ContactPermissions.Groups.Delete => "Delete groups",
            ContactPermissions.Groups.Assign => "Assign groups",
            _ => $"Permission: {permission}"
        };
    }
}