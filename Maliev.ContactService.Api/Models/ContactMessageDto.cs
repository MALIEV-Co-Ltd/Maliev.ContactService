using Maliev.ContactService.Data.Models;

namespace Maliev.ContactService.Api.Models;

/// <summary>
/// Data Transfer Object for a contact message.
/// </summary>
public class ContactMessageDto
{
    /// <summary>
    /// The unique identifier of the contact message.
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// The full name of the sender.
    /// </summary>
    public required string FullName { get; set; }
    /// <summary>
    /// The email address of the sender.
    /// </summary>
    public required string Email { get; set; }
    /// <summary>
    /// The phone number of the sender (optional).
    /// </summary>
    public string? PhoneNumber { get; set; }
    /// <summary>
    /// The company of the sender (optional).
    /// </summary>
    public string? Company { get; set; }
    /// <summary>
    /// The subject of the contact message.
    /// </summary>
    public required string Subject { get; set; }
    /// <summary>
    /// The content of the contact message.
    /// </summary>
    public required string Message { get; set; }
    /// <summary>
    /// The ID of the country from which the message was sent.
    /// </summary>
    public int CountryId { get; set; }
    /// <summary>
    /// The type of contact (e.g., General Inquiry, Support).
    /// </summary>
    public ContactType ContactType { get; set; }
    /// <summary>
    /// The priority level of the contact message.
    /// </summary>
    public Priority Priority { get; set; }
    /// <summary>
    /// The current status of the contact message.
    /// </summary>
    public ContactStatus Status { get; set; }
    /// <summary>
    /// The timestamp when the contact message was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>
    /// The timestamp when the contact message was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
    /// <summary>
    /// The timestamp when the contact message was resolved (optional).
    /// </summary>
    public DateTimeOffset? ResolvedAt { get; set; }
    /// <summary>
    /// A list of files associated with the contact message.
    /// </summary>
    public List<ContactFileDto> Files { get; set; } = new();
}

/// <summary>
/// Data Transfer Object for a file associated with a contact message.
/// </summary>
public class ContactFileDto
{
    /// <summary>
    /// The unique identifier of the file.
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// The name of the file.
    /// </summary>
    public required string FileName { get; set; }
    /// <summary>
    /// The object name of the file in the storage service.
    /// </summary>
    public required string ObjectName { get; set; }
    /// <summary>
    /// The size of the file in bytes (optional).
    /// </summary>
    public long? FileSize { get; set; }
    /// <summary>
    /// The content type of the file (e.g., "image/jpeg", "application/pdf").
    /// </summary>
    public string? ContentType { get; set; }
    /// <summary>
    /// The unique identifier of the file in the Upload Service (optional).
    /// </summary>
    public string? UploadServiceFileId { get; set; }
    /// <summary>
    /// The timestamp when the file was uploaded.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
