using Maliev.ContactService.Application.DTOs;
using Maliev.ContactService.Domain.Entities;

namespace Maliev.ContactService.Application.Interfaces;

public interface IContactService
{
    Task<ContactMessageDto> CreateContactMessageAsync(
        CreateContactMessageRequest request,
        CancellationToken cancellationToken = default);
    Task<ContactMessageDto?> GetContactMessageByIdAsync(int id);
    Task<IEnumerable<ContactMessageDto>> GetContactMessagesAsync(int page = 1, int pageSize = 20, ContactStatus? status = null, ContactType? contactType = null, string? email = null);
    Task<ContactMessageDto> UpdateContactStatusAsync(int id, UpdateContactStatusRequest request);
    Task DeleteContactMessageAsync(int id);
    Task<IEnumerable<ContactFileDto>> GetContactFilesAsync(int contactId);
    Task<ContactFileDto?> GetContactFileByIdAsync(int contactId, int fileId);
    Task DeleteContactFileAsync(
        int contactId,
        int fileId,
        CancellationToken cancellationToken = default);
}
