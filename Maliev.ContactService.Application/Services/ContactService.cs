using Maliev.ContactService.Application.DTOs;
using Maliev.ContactService.Application.Exceptions;
using Maliev.ContactService.Application.Interfaces;
using Maliev.ContactService.Application.Mappings;
using Maliev.ContactService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Maliev.ContactService.Application.Services;

/// <summary>
/// Service for Contact operations
/// </summary>
public class ContactService : IContactService
{
    private readonly IContactDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly IUploadServiceClient _uploadService;
    private readonly ICountryServiceClient _countryService;
    private readonly ILogger<ContactService> _logger;
    private static readonly DistributedCacheEntryOptions CacheOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
    private const string ListCacheVersionKey = "contact-list-version";

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactService"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="cache">The distributed cache instance.</param>
    /// <param name="uploadService">The upload service client.</param>
    /// <param name="countryService">The country service client.</param>
    /// <param name="logger">The logger instance.</param>
    public ContactService(
        IContactDbContext context,
        IDistributedCache cache,
        IUploadServiceClient uploadService,
        ICountryServiceClient countryService,
        ILogger<ContactService> logger)
    {
        _context = context;
        _cache = cache;
        _uploadService = uploadService;
        _countryService = countryService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ContactMessageDto> CreateContactMessageAsync(CreateContactMessageRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var sixtySecondsAgo = DateTimeOffset.UtcNow.AddSeconds(-60);
        var strategy = _context.Database.CreateExecutionStrategy();

        var isDuplicate = await strategy.ExecuteAsync(async () =>
            await _context.ContactMessages.AnyAsync(c => c.Email == request.Email && c.CreatedAt > sixtySecondsAgo));

        if (isDuplicate)
        {
            _logger.LogWarning("Duplicate inquiry detected for email {Email} within 60 seconds", request.Email);
            throw new DuplicateInquiryException();
        }

        if (!await _countryService.ValidateCountryExistsAsync(request.CountryId.ToString()))
        {
            _logger.LogWarning("Invalid or inactive country ID {CountryId} for email {Email}", request.CountryId, request.Email);
            throw new ArgumentException($"Country ID {request.CountryId} is not valid or is not currently accepting inquiries.", nameof(request.CountryId));
        }

        _logger.LogInformation("Creating contact inquiry for {Email} with country ID {CountryId}", request.Email, request.CountryId);

        var createdMessage = await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
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
                    CountryId = request.CountryId,
                    ContactType = request.ContactType,
                    Priority = request.Priority,
                    Status = ContactStatus.New,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                _context.ContactMessages.Add(contactMessage);
                await _context.SaveChangesAsync();

                if (request.Files.Any())
                {
                    var uploadedFiles = new List<ContactFile>();
                    foreach (var fileRequest in request.Files)
                    {
                        try
                        {
                            var objectName = $"contacts/{contactMessage.Id}/{Guid.NewGuid()}_{fileRequest.FileName}";
                            var uploadResponse = await _uploadService.UploadFileAsync(objectName, fileRequest.FileContent, fileRequest.ContentType ?? "application/octet-stream", fileRequest.FileName);

                            var contactFile = new ContactFile
                            {
                                ContactMessageId = contactMessage.Id,
                                FileName = fileRequest.FileName,
                                ObjectName = objectName,
                                FileSize = uploadResponse.FileSize,
                                ContentType = fileRequest.ContentType ?? "application/octet-stream",
                                UploadServiceFileId = uploadResponse.FileId,
                                CreatedAt = DateTimeOffset.UtcNow
                            };
                            uploadedFiles.Add(contactFile);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to upload file {FileName} for contact {ContactId}, continuing submission.", fileRequest.FileName, contactMessage.Id);
                        }
                    }

                    if (uploadedFiles.Any())
                    {
                        _context.ContactFiles.AddRange(uploadedFiles);
                        await _context.SaveChangesAsync();
                    }
                }

                await transaction.CommitAsync();

                _logger.LogInformation("Contact inquiry created successfully. ID={ContactId}", contactMessage.Id);
                return contactMessage.ToDto();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to create contact inquiry for email {Email}. Transaction rolled back.", request.Email);
                throw;
            }
        });

        await InvalidateListCacheAsync();
        return createdMessage;
    }

    /// <inheritdoc/>
    public async Task<ContactMessageDto?> GetContactMessageByIdAsync(int id)
    {
        var cacheKey = $"contact_message_{id}";
        var cachedBytes = await _cache.GetAsync(cacheKey);
        if (cachedBytes != null && cachedBytes.Length > 0)
        {
            return JsonSerializer.Deserialize<ContactMessageDto>(Encoding.UTF8.GetString(cachedBytes));
        }

        var contact = await _context.ContactMessages.Include(c => c.Files).AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (contact == null) return null;

        var dto = contact.ToDto();
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(dto), CacheOptions);
        return dto;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ContactMessageDto>> GetContactMessagesAsync(int page = 1, int pageSize = 20, ContactStatus? status = null, ContactType? contactType = null, string? email = null)
    {
        var version = await GetCacheVersionAsync();
        string cacheKey = $"contact_messages_v{version}_p{page}_s{pageSize}_st{status}_ct{contactType}_e{email}";

        var cachedBytes = await _cache.GetAsync(cacheKey);
        if (cachedBytes != null && cachedBytes.Length > 0)
        {
            return JsonSerializer.Deserialize<IEnumerable<ContactMessageDto>>(Encoding.UTF8.GetString(cachedBytes)) ?? Enumerable.Empty<ContactMessageDto>();
        }

        var query = _context.ContactMessages.Include(c => c.Files).AsNoTracking();
        if (status.HasValue) query = query.Where(c => c.Status == status.Value);
        if (contactType.HasValue) query = query.Where(c => c.ContactType == contactType.Value);
        if (!string.IsNullOrWhiteSpace(email)) query = query.Where(c => c.Email.ToLower() == email.ToLower());

        var contacts = await query.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var dtos = contacts.Select(c => c.ToDto()).ToList();

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(dtos), CacheOptions);
        return dtos;
    }

    /// <inheritdoc/>
    public async Task<ContactMessageDto> UpdateContactStatusAsync(int id, UpdateContactStatusRequest request)
    {
        var contact = await _context.ContactMessages.FindAsync(id);
        if (contact == null) throw new NotFoundException($"Contact message with id {id} not found");

        if (contact.UpdatedAt > DateTimeOffset.UtcNow.AddMinutes(-5))
        {
            _logger.LogWarning("Potential concurrent update for contact {ContactId}", id);
        }

        contact.Status = request.Status;
        if (request.Priority.HasValue) contact.Priority = request.Priority.Value;
        contact.UpdatedAt = DateTimeOffset.UtcNow;
        if (request.Status == ContactStatus.Resolved && !contact.ResolvedAt.HasValue)
        {
            contact.ResolvedAt = DateTimeOffset.UtcNow;
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency conflict updating contact {ContactId}", id);
            throw new InvalidOperationException("The contact message was modified by another user. Please refresh and try again.", ex);
        }

        await InvalidateListCacheAsync();
        await _cache.RemoveAsync($"contact_message_{id}");
        _logger.LogInformation("Updated contact message {ContactId} status to {Status}", id, request.Status);

        return contact.ToDto();
    }

    /// <inheritdoc/>
    public async Task DeleteContactMessageAsync(int id)
    {
        var contact = await _context.ContactMessages.FindAsync(id);
        if (contact == null) throw new NotFoundException($"Contact message with id {id} not found");

        _context.ContactMessages.Remove(contact);
        await _context.SaveChangesAsync();

        await InvalidateListCacheAsync();
        await _cache.RemoveAsync($"contact_message_{id}");
        _logger.LogInformation("Deleted contact message {ContactId}", id);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ContactFileDto>> GetContactFilesAsync(int contactId)
    {
        return await _context.ContactFiles.Where(f => f.ContactMessageId == contactId).AsNoTracking()
            .Select(f => f.ToDto()).ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<ContactFileDto?> GetContactFileByIdAsync(int contactId, int fileId)
    {
        var file = await _context.ContactFiles.AsNoTracking()
            .FirstOrDefaultAsync(f => f.ContactMessageId == contactId && f.Id == fileId);

        return file?.ToDto();
    }

    /// <inheritdoc/>
    public async Task DeleteContactFileAsync(int contactId, int fileId)
    {
        var file = await _context.ContactFiles.FirstOrDefaultAsync(f => f.Id == fileId && f.ContactMessageId == contactId);
        if (file == null) throw new NotFoundException($"Contact file with id {fileId} for contact {contactId} not found");

        if (!string.IsNullOrEmpty(file.UploadServiceFileId))
        {
            try
            {
                await _uploadService.DeleteFileAsync(file.UploadServiceFileId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete file from UploadService: {UploadServiceFileId}", file.UploadServiceFileId);
            }
        }

        _context.ContactFiles.Remove(file);
        await _context.SaveChangesAsync();

        await InvalidateListCacheAsync();
        await _cache.RemoveAsync($"contact_message_{contactId}");
        _logger.LogInformation("Deleted contact file {FileId} from contact {ContactId}", fileId, contactId);
    }

    private async Task<long> GetCacheVersionAsync()
    {
        var versionBytes = await _cache.GetAsync(ListCacheVersionKey);
        return (versionBytes == null || versionBytes.Length < 8) ? 1 : BitConverter.ToInt64(versionBytes, 0);
    }

    private async Task InvalidateListCacheAsync()
    {
        var currentVersion = await GetCacheVersionAsync();
        var nextVersion = currentVersion + 1;
        await _cache.SetAsync(ListCacheVersionKey, BitConverter.GetBytes(nextVersion), CacheOptions);
        _logger.LogInformation("Contact list cache invalidated. Version incremented to {Version}", nextVersion);
    }
}
