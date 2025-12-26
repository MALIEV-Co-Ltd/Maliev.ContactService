namespace Maliev.ContactService.Api.Services.Auth;

/// <summary>
/// Defines all permissions used in the Contact Service.
/// </summary>
public static class ContactPermissions
{
    /// <summary>
    /// Permissions related to contact management.
    /// </summary>
    public static class Contacts
    {
        /// <summary>Permission to create contacts.</summary>
        public const string Create = "contact.contacts.create";
        /// <summary>Permission to read contacts.</summary>
        public const string Read = "contact.contacts.read";
        /// <summary>Permission to update contacts.</summary>
        public const string Update = "contact.contacts.update";
        /// <summary>Permission to delete contacts.</summary>
        public const string Delete = "contact.contacts.delete";
        /// <summary>Permission to merge contacts.</summary>
        public const string Merge = "contact.contacts.merge";
        /// <summary>Permission to export contacts.</summary>
        public const string Export = "contact.contacts.export";
    }

    /// <summary>
    /// Permissions related to communications.
    /// </summary>
    public static class Communications
    {
        /// <summary>Permission to log communications.</summary>
        public const string Log = "contact.communications.log";
        /// <summary>Permission to read communications.</summary>
        public const string Read = "contact.communications.read";
        /// <summary>Permission to delete communications.</summary>
        public const string Delete = "contact.communications.delete";
    }

    /// <summary>
    /// Permissions related to contact groups.
    /// </summary>
    public static class Groups
    {
        /// <summary>Permission to create groups.</summary>
        public const string Create = "contact.groups.create";
        /// <summary>Permission to read groups.</summary>
        public const string Read = "contact.groups.read";
        /// <summary>Permission to update groups.</summary>
        public const string Update = "contact.groups.update";
        /// <summary>Permission to delete groups.</summary>
        public const string Delete = "contact.groups.delete";
        /// <summary>Permission to assign groups.</summary>
        public const string Assign = "contact.groups.assign";
    }

    /// <summary>
    /// Gets all registered permissions.
    /// </summary>
    public static IEnumerable<string> GetAll()
    {
        return new[]
        {
            Contacts.Create, Contacts.Read, Contacts.Update, Contacts.Delete, Contacts.Merge, Contacts.Export,
            Communications.Log, Communications.Read, Communications.Delete,
            Groups.Create, Groups.Read, Groups.Update, Groups.Delete, Groups.Assign
        };
    }
}
