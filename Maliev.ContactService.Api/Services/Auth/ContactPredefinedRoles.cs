namespace Maliev.ContactService.Api.Services.Auth;

/// <summary>
/// Predefined roles for the Contact Service.
/// </summary>
public static class ContactPredefinedRoles
{
    /// <summary>Role for administrators with full access.</summary>
    public const string Admin = "roles.contact.admin";
    /// <summary>Role for managers with broad access.</summary>
    public const string Manager = "roles.contact.manager";
    /// <summary>Standard user role for contact management.</summary>
    public const string User = "roles.contact.user";
    /// <summary>Role for users with read-only access.</summary>
    public const string Viewer = "roles.contact.viewer";

    /// <summary>
    /// Collection of all predefined roles for the Contact Service.
    /// </summary>
    public static readonly IReadOnlyList<(string RoleId, string Description, string[] Permissions)> All = new List<(string, string, string[])>
    {
        (Admin, "Full access to all contact operations", ContactPermissions.GetAll().ToArray()),
        (Manager, "Most contact access (cannot merge)", ContactPermissions.GetAll()
            .Where(p => p != ContactPermissions.Contacts.Merge)
            .ToArray()),
        (User, "Standard contact access", new[]
        {
            ContactPermissions.Contacts.Create,
            ContactPermissions.Contacts.Read,
            ContactPermissions.Contacts.Update,
            ContactPermissions.Communications.Log,
            ContactPermissions.Communications.Read
        }),
        (Viewer, "Read-only access to contacts", new[]
        {
            ContactPermissions.Contacts.Read,
            ContactPermissions.Communications.Read,
            ContactPermissions.Groups.Read
        })
    };

    /// <summary>
    /// Gets the permissions associated with a specific role.
    /// </summary>
    /// <param name="roleId">The role identifier.</param>
    /// <returns>A collection of permission identifiers.</returns>
    public static IEnumerable<string> GetPermissionsForRole(string roleId)
    {
        return All.FirstOrDefault(r => r.RoleId == roleId).Permissions ?? Enumerable.Empty<string>();
    }
}
