using Maliev.ContactService.Data.Models;

namespace Maliev.ContactService.Api.Models;

public class ContactMessageDto
{
    public int Id { get; set; }
    public required string FullName { get; set; }
    public required string Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Company { get; set; }
    public required string Subject { get; set; }
    public required string Message { get; set; }
    public ContactType ContactType { get; set; }
    public Priority Priority { get; set; }
    public ContactStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public List<ContactFileDto> Files { get; set; } = new();
}

public class ContactFileDto
{
    public int Id { get; set; }
    public required string FileName { get; set; }
    public required string ObjectName { get; set; }
    public long? FileSize { get; set; }
    public string? ContentType { get; set; }
    public string? UploadServiceFileId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}