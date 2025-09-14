using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Data.Models;

namespace Maliev.ContactService.Api.Services;

public interface IContactService
{
    Task<ContactMessageDto> CreateContactMessageAsync(CreateContactMessageRequest request);
    Task<ContactMessageDto?> GetContactMessageByIdAsync(int id);
    Task<IEnumerable<ContactMessageDto>> GetContactMessagesAsync(
        int page = 1,
        int pageSize = 20,
        ContactStatus? status = null,
        ContactType? contactType = null);
    Task<ContactMessageDto?> UpdateContactStatusAsync(int id, UpdateContactStatusRequest request);
    Task<bool> DeleteContactMessageAsync(int id);
    Task<IEnumerable<ContactFileDto>> GetContactFilesAsync(int contactId);
    Task<bool> DeleteContactFileAsync(int contactId, int fileId);
}