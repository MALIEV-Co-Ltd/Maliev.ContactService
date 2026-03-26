namespace Maliev.ContactService.Application.Authorization;

/// <summary>
/// Provides access to predefined roles for the Contact Service.
/// </summary>
public static class ContactPredefinedRoles
{
    public const string Admin = "roles.contact.admin";
    public const string Operator = "roles.contact.operator";
    public const string Viewer = "roles.contact.viewer";

    public static readonly IReadOnlyList<(string RoleId, string Description, string[] Permissions)> All = new List<(string, string, string[])>
    {
        (
            Admin,
            "Contact Administrator with full access",
            new[]
            {
                ContactPermissions.MessageCreate,
                ContactPermissions.MessageRead,
                ContactPermissions.MessageUpdate,
                ContactPermissions.MessageDelete,
                ContactPermissions.FileUpload,
                ContactPermissions.FileRead,
                ContactPermissions.FileDelete,
                ContactPermissions.NoteCreate,
                ContactPermissions.NoteRead,
                ContactPermissions.NoteUpdate,
                ContactPermissions.NoteDelete,
                ContactPermissions.NdaCreate,
                ContactPermissions.NdaRead,
                ContactPermissions.NdaSign,
                ContactPermissions.NdaApprove,
            }
        ),
        (
            Operator,
            "Contact Operator with message and note access",
            new[]
            {
                ContactPermissions.MessageCreate,
                ContactPermissions.MessageRead,
                ContactPermissions.MessageUpdate,
                ContactPermissions.FileUpload,
                ContactPermissions.FileRead,
                ContactPermissions.NoteCreate,
                ContactPermissions.NoteRead,
                ContactPermissions.NoteUpdate,
                ContactPermissions.NdaRead,
                ContactPermissions.NdaSign,
            }
        ),
        (
            Viewer,
            "Contact Viewer with read-only access",
            new[]
            {
                ContactPermissions.MessageRead,
                ContactPermissions.FileRead,
                ContactPermissions.NoteRead,
                ContactPermissions.NdaRead,
            }
        ),
    };
}
