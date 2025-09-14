using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Maliev.ContactService.Api.Services;

public class ContactService : IContactService
{
    private readonly ContactDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IUploadServiceClient _uploadService;
    private readonly ILogger<ContactService> _logger;
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);

    public ContactService(ContactDbContext context, IMemoryCache cache, IUploadServiceClient uploadService, ILogger<ContactService> logger)
    {
        _context = context;
        _cache = cache;
        _uploadService = uploadService;
        _logger = logger;
    }

    public async Task<ContactMessageDto> CreateContactMessageAsync(CreateContactMessageRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        // Check if the database provider supports transactions
        var isTransactionSupported = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
        var transaction = isTransactionSupported ? await _context.Database.BeginTransactionAsync() : null;

        try
        {
            var contactMessage = new ContactMessage
            {
                FullName = request.FullName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                Company = request.Company,
                Subject = request.Subject,
                Message = request.Message,
                ContactType = request.ContactType,
                Priority = request.Priority,
                Status = ContactStatus.New,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ContactMessages.Add(contactMessage);
            await _context.SaveChangesAsync();

            // Add files if any
            if (request.Files.Any())
            {
                foreach (var fileRequest in request.Files)
                {
                    try
                    {
                        // Generate object name: contacts/{contactId}/{timestamp}_{fileName}
                        var objectName = $"contacts/{contactMessage.Id}/{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{fileRequest.FileName}";

                        // Upload file to UploadService
                        var uploadResponse = await _uploadService.UploadFileAsync(
                            objectName,
                            fileRequest.FileContent,
                            fileRequest.ContentType ?? "application/octet-stream",
                            fileRequest.FileName);

                        // Store file metadata in database
                        var contactFile = new ContactFile
                        {
                            ContactMessageId = contactMessage.Id,
                            FileName = fileRequest.FileName,
                            ObjectName = objectName,
                            FileSize = uploadResponse.FileSize,
                            ContentType = fileRequest.ContentType ?? "application/octet-stream",
                            UploadServiceFileId = uploadResponse.FileId,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.ContactFiles.Add(contactFile);
                        _logger.LogInformation("File uploaded for contact {ContactId}: {FileName} -> {FileId}",
                            contactMessage.Id, fileRequest.FileName, uploadResponse.FileId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to upload file {FileName} for contact {ContactId}",
                            fileRequest.FileName, contactMessage.Id);

                        // Continue with other files - don't fail the entire contact creation
                        // You might want to store failed uploads with a different status
                    }
                }

                await _context.SaveChangesAsync();
            }

            if (transaction != null)
            {
                await transaction.CommitAsync();
            }
            
            _logger.LogInformation("Created contact message {ContactId} from {Email}", contactMessage.Id, contactMessage.Email);

            return await GetContactMessageByIdAsync(contactMessage.Id) ?? throw new InvalidOperationException("Failed to retrieve created contact message");
        }
        catch (Exception ex)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }
            _logger.LogError(ex, "Failed to create contact message");
            throw; // Re-throw the exception to be handled by the global exception handler
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<ContactMessageDto?> GetContactMessageByIdAsync(int id)
    {
        var cacheKey = $"contact_message_{id}";

        if (_cache.TryGetValue(cacheKey, out ContactMessageDto? cachedContact))
        {
            return cachedContact;
        }

        var contact = await _context.ContactMessages
            .Include(c => c.Files)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contact == null) return null;

        var contactDto = MapToDto(contact);
        _cache.Set(cacheKey, contactDto, CacheExpiry);

        return contactDto;
    }

    public async Task<IEnumerable<ContactMessageDto>> GetContactMessagesAsync(
        int page = 1,
        int pageSize = 20,
        ContactStatus? status = null,
        ContactType? contactType = null)
    {
        var query = _context.ContactMessages
            .Include(c => c.Files)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        if (contactType.HasValue)
            query = query.Where(c => c.ContactType == contactType.Value);

        var contacts = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return contacts.Select(MapToDto);
    }

    public async Task<ContactMessageDto> UpdateContactStatusAsync(int id, UpdateContactStatusRequest request)
    {
        var contact = await _context.ContactMessages.FindAsync(id);
        if (contact == null) 
            throw new Maliev.ContactService.Api.Exceptions.NotFoundException($"Contact message with id {id} not found");

        contact.Status = request.Status;
        if (request.Priority.HasValue)
            contact.Priority = request.Priority.Value;

        contact.UpdatedAt = DateTime.UtcNow;

        if (request.Status == ContactStatus.Resolved && !contact.ResolvedAt.HasValue)
        {
            contact.ResolvedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        // Clear cache
        _cache.Remove($"contact_message_{id}");

        _logger.LogInformation("Updated contact message {ContactId} status to {Status}", id, request.Status);

        return await GetContactMessageByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve updated contact message");
    }

    public async Task DeleteContactMessageAsync(int id)
    {
        var contact = await _context.ContactMessages.FindAsync(id);
        if (contact == null) 
            throw new Maliev.ContactService.Api.Exceptions.NotFoundException($"Contact message with id {id} not found");

        _context.ContactMessages.Remove(contact);
        await _context.SaveChangesAsync();

        // Clear cache
        _cache.Remove($"contact_message_{id}");

        _logger.LogInformation("Deleted contact message {ContactId}", id);
    }

    public async Task<IEnumerable<ContactFileDto>> GetContactFilesAsync(int contactId)
    {
        var files = await _context.ContactFiles
            .Where(f => f.ContactMessageId == contactId)
            .ToListAsync();

        return files.Select(f => new ContactFileDto
        {
            Id = f.Id,
            FileName = f.FileName,
            ObjectName = f.ObjectName,
            FileSize = f.FileSize,
            ContentType = f.ContentType,
            UploadServiceFileId = f.UploadServiceFileId,
            CreatedAt = f.CreatedAt
        });
    }

    public async Task DeleteContactFileAsync(int contactId, int fileId)
    {
        var file = await _context.ContactFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && f.ContactMessageId == contactId);

        if (file == null) 
            throw new Maliev.ContactService.Api.Exceptions.NotFoundException($"Contact file with id {fileId} for contact {contactId} not found");

        // Delete from UploadService first (if we have the file ID)
        if (!string.IsNullOrEmpty(file.UploadServiceFileId))
        {
            try
            {
                await _uploadService.DeleteFileAsync(file.UploadServiceFileId);
                _logger.LogInformation("Deleted file from UploadService: {UploadServiceFileId}", file.UploadServiceFileId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete file from UploadService: {UploadServiceFileId}", file.UploadServiceFileId);
                // Continue with database deletion even if UploadService fails
            }
        }

        // Delete from database
        _context.ContactFiles.Remove(file);
        await _context.SaveChangesAsync();

        // Clear parent contact cache
        _cache.Remove($"contact_message_{contactId}");

        _logger.LogInformation("Deleted contact file {FileId} from contact {ContactId}", fileId, contactId);
    }

    private static ContactMessageDto MapToDto(ContactMessage contact)
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
            ContactType = contact.ContactType,
            Priority = contact.Priority,
            Status = contact.Status,
            CreatedAt = contact.CreatedAt,
            UpdatedAt = contact.UpdatedAt,
            ResolvedAt = contact.ResolvedAt,
            Files = contact.Files.Select(f => new ContactFileDto
            {
                Id = f.Id,
                FileName = f.FileName,
                ObjectName = f.ObjectName,
                FileSize = f.FileSize,
                ContentType = f.ContentType,
                UploadServiceFileId = f.UploadServiceFileId,
                CreatedAt = f.CreatedAt
            }).ToList()
        };
    }
}