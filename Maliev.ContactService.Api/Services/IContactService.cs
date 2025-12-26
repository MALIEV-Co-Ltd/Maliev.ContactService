using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Data.Models;

namespace Maliev.ContactService.Api.Services;
/// <summary>
/// Service interface for Contact operations
/// </summary>

public interface IContactService
{
    /// <summary>
    /// Creates a new contact message.
    /// </summary>
    /// <param name="request">The contact message creation request.</param>
    /// <returns>The created contact message.</returns>
    Task<ContactMessageDto> CreateContactMessageAsync(CreateContactMessageRequest request);

    /// <summary>
    /// Gets a contact message by its identifier.
    /// </summary>
    /// <param name="id">The contact message identifier.</param>
    /// <returns>The contact message if found, null otherwise.</returns>
    Task<ContactMessageDto?> GetContactMessageByIdAsync(int id);

    /// <summary>
    /// Gets a paginated list of contact messages with optional filtering.
    /// </summary>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="contactType">Optional contact type filter.</param>
    /// <param name="email">Optional email filter.</param>
    /// <returns>A collection of contact messages.</returns>
    Task<IEnumerable<ContactMessageDto>> GetContactMessagesAsync(
        int page = 1,
        int pageSize = 20,
        ContactStatus? status = null,
        ContactType? contactType = null,
        string? email = null);

    /// <summary>
    /// Updates the status of a contact message.
    /// </summary>
    /// <param name="id">The contact message identifier.</param>
    /// <param name="request">The status update request.</param>
    /// <returns>The updated contact message.</returns>
    Task<ContactMessageDto> UpdateContactStatusAsync(int id, UpdateContactStatusRequest request);

    /// <summary>
    /// Deletes a contact message.
    /// </summary>
    /// <param name="id">The contact message identifier.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteContactMessageAsync(int id);

    /// <summary>
    /// Gets all files associated with a contact message.
    /// </summary>
    /// <param name="contactId">The contact message identifier.</param>
    /// <returns>A collection of contact files.</returns>
    Task<IEnumerable<ContactFileDto>> GetContactFilesAsync(int contactId);

    /// <summary>
    /// Gets a specific file by its identifier.
    /// </summary>
    /// <param name="contactId">The contact message identifier.</param>
    /// <param name="fileId">The file identifier.</param>
    /// <returns>The contact file if found, null otherwise.</returns>
    Task<ContactFileDto?> GetContactFileByIdAsync(int contactId, int fileId);

    /// <summary>
    /// Deletes a file from a contact message.
    /// </summary>
    /// <param name="contactId">The contact message identifier.</param>
    /// <param name="fileId">The file identifier.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteContactFileAsync(int contactId, int fileId);
}
