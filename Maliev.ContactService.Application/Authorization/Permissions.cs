namespace Maliev.ContactService.Application.Authorization;

/// <summary>
/// Defines the permissions for the Contact Service.
/// </summary>
public static class ContactPermissions
{
    public const string MessageCreate = "contact.messages.create";
    public const string MessageRead = "contact.messages.read";
    public const string MessageUpdate = "contact.messages.update";
    public const string MessageDelete = "contact.messages.delete";

    public const string FileUpload = "contact.files.upload";
    public const string FileRead = "contact.files.read";
    public const string FileDelete = "contact.files.delete";

    public const string NoteCreate = "contact.notes.create";
    public const string NoteRead = "contact.notes.read";
    public const string NoteUpdate = "contact.notes.update";
    public const string NoteDelete = "contact.notes.delete";

    public const string NdaCreate = "contact.ndas.create";
    public const string NdaRead = "contact.ndas.read";
    public const string NdaSign = "contact.ndas.sign";
    public const string NdaApprove = "contact.ndas.approve";

    public static readonly IReadOnlyDictionary<string, string> AllWithDescriptions = new Dictionary<string, string>
    {
        { MessageCreate, "Create contact messages" },
        { MessageRead, "Read contact messages" },
        { MessageUpdate, "Update contact messages" },
        { MessageDelete, "Delete contact messages" },
        { FileUpload, "Upload contact files" },
        { FileRead, "Read contact files" },
        { FileDelete, "Delete contact files" },
        { NoteCreate, "Create contact notes" },
        { NoteRead, "Read contact notes" },
        { NoteUpdate, "Update contact notes" },
        { NoteDelete, "Delete contact notes" },
        { NdaCreate, "Create NDAs" },
        { NdaRead, "Read NDAs" },
        { NdaSign, "Sign NDAs" },
        { NdaApprove, "Approve NDAs" },
    };

    public static string[] All => AllWithDescriptions.Keys.ToArray();
}
