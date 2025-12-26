namespace Maliev.ContactService.Api.Services.Auth;

/// <summary>
/// Defines predefined roles and their associated permissions.
/// Roles follow the GCP format: roles.{service}.{role-name}
/// </summary>
public static class ContactPredefinedRoles
{
    /// <summary>Admin role with full access (GCP format, was: contact-admin).</summary>
    public const string Admin = "roles.contact.admin";
    /// <summary>Manager role with most access (GCP format, was: contact-manager).</summary>
    public const string Manager = "roles.contact.manager";
    /// <summary>User role with standard access (GCP format, was: contact-user).</summary>
    public const string User = "roles.contact.user";
    /// <summary>Viewer role with read-only access (GCP format, was: contact-viewer).</summary>
    public const string Viewer = "roles.contact.viewer";

    /// <summary>
    /// Gets the permissions associated with a specific role.
    /// </summary>
    public static IEnumerable<string> GetPermissionsForRole(string roleName)
    {
        return roleName switch
        {
            Admin => ContactPermissions.GetAll(),
            Manager => ContactPermissions.GetAll().Where(p => p != ContactPermissions.Contacts.Merge),
            User => new[]
            {
                ContactPermissions.Contacts.Create,
                ContactPermissions.Contacts.Read,
                ContactPermissions.Contacts.Update,
                ContactPermissions.Communications.Log,
                ContactPermissions.Communications.Read
            },
            Viewer => new[]
            {
                ContactPermissions.Contacts.Read,
                ContactPermissions.Communications.Read,
                ContactPermissions.Groups.Read
            },
            _ => Enumerable.Empty<string>()
        };
    }

    /// <summary>
    /// Gets all predefined roles.
    /// </summary>
    public static IEnumerable<string> GetAll()
    {
        return new[] { Admin, Manager, User, Viewer };
    }
}
