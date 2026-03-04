using Maliev.ContactService.Application.DTOs;
using Maliev.ContactService.Domain.Entities;

namespace Maliev.ContactService.Application.Mappings;

public static class ContactMapper
{
    public static ContactMessageDto ToDto(this ContactMessage contact)
    {
        return new ContactMessageDto
        {
            Id = contact.Id,
            FullName = contact.FullName,
            Email = contact.Email,
            PhoneNumber = contact.PhoneNumber,
            Company = contact.Company,
            Subject = contact.Subject,
            Message = contact.Message,
            CountryId = contact.CountryId,
            ContactType = contact.ContactType,
            Priority = contact.Priority,
            Status = contact.Status,
            CreatedAt = contact.CreatedAt,
            UpdatedAt = contact.UpdatedAt,
            ResolvedAt = contact.ResolvedAt,
            Files = contact.Files.Select(f => f.ToDto()).ToList()
        };
    }

    public static ContactFileDto ToDto(this ContactFile f)
    {
        return new ContactFileDto
        {
            Id = f.Id,
            FileName = f.FileName,
            ObjectName = f.ObjectName,
            FileSize = f.FileSize,
            ContentType = f.ContentType,
            UploadServiceFileId = f.UploadServiceFileId,
            CreatedAt = f.CreatedAt
        };
    }
}
