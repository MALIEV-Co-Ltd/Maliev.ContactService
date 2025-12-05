using Maliev.ContactService.Api.Exceptions;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Data.DbContexts;
using Maliev.ContactService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace Maliev.ContactService.Api.Services;
/// <summary>
/// Service for Contact operations
/// </summary>

public class ContactService : IContactService
{
    private readonly ContactDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IUploadServiceClient _uploadService;
    private readonly ICountryServiceClient _countryService;
    private readonly ILogger<ContactService> _logger;
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);
    private static CancellationTokenSource _cacheCts = new CancellationTokenSource();
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactService"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="cache">The memory cache instance.</param>
    /// <param name="uploadService">The upload service client.</param>
    /// <param name="countryService">The country service client.</param>
    /// <param name="logger">The logger instance.</param>
    public ContactService(
        ContactDbContext context,
        IMemoryCache cache,
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
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        // FR-022: Check for duplicate inquiries from same email within 60 seconds
        // This prevents spam and accidental duplicate submissions by checking if the same email
        // has submitted an inquiry within the last 60 seconds. Uses the IX_ContactMessages_Email_CreatedAt
        // composite index for efficient querying. Returns 409 Conflict if duplicate is detected.
        var sixtySecondsAgo = DateTimeOffset.UtcNow.AddSeconds(-60);
        var hasDuplicateInquiry = await _context.ContactMessages
            .AnyAsync(c => c.Email == request.Email && c.CreatedAt > sixtySecondsAgo);

        if (hasDuplicateInquiry)
        {
            _logger.LogWarning("Duplicate inquiry detected for email {Email} within 60 seconds", request.Email);
            throw new DuplicateInquiryException();
        }

        // FR-007: Validate country exists and is active via Country Service
        bool isValidCountry = await _countryService.ValidateCountryExistsAsync(request.CountryId);
        if (!isValidCountry)
        {
            _logger.LogWarning("Invalid or inactive country ID {CountryId} for email {Email}", request.CountryId, request.Email);
            throw new ArgumentException($"Country ID {request.CountryId} is not valid or is not currently accepting inquiries.", nameof(request.CountryId));
        }

        _logger.LogInformation("Creating contact inquiry for {Email} with country ID {CountryId}", request.Email, request.CountryId);

        // Use execution strategy for retry-compatible transaction handling
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            // Check if the database provider supports transactions
            var isTransactionSupported = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";

            using var transaction = isTransactionSupported ? await _context.Database.BeginTransactionAsync() : null;
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
                                CreatedAt = DateTimeOffset.UtcNow
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

                // FR-026: Audit logging for successful inquiry
                _logger.LogInformation(
                    "Contact inquiry created successfully. ID={ContactId}, Email={Email}, CountryId={CountryId}, Type={ContactType}, FileCount={FileCount}",
                    contactMessage.Id, contactMessage.Email, contactMessage.CountryId, contactMessage.ContactType, request.Files.Count);

                return await GetContactMessageByIdAsync(contactMessage.Id) ?? throw new InvalidOperationException("Failed to retrieve created contact message");
            }
            catch (DuplicateInquiryException ex)
            {
                // FR-026: Audit logging for duplicate inquiry attempt
                _logger.LogWarning("Duplicate inquiry attempt blocked. Email={Email}, Reason={Reason}", request.Email, ex.Message);
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                throw;
            }
            catch (CountryServiceException ex)
            {
                // FR-026: Audit logging for Country Service failure
                _logger.LogError(ex, "Country Service unavailable during inquiry creation. Email={Email}, CountryId={CountryId}",
                    request.Email, request.CountryId);
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                throw;
            }
            catch (Exception ex)
            {
                // FR-026: Audit logging for failed inquiry
                _logger.LogError(ex, "Failed to create contact inquiry. Email={Email}, CountryId={CountryId}, Error={Error}",
                    request.Email, request.CountryId, ex.Message);
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                throw; // Re-throw the exception to be handled by the global exception handler
            }
        });
    }

    /// <inheritdoc/>
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
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(CacheExpiry)
            .AddExpirationToken(new CancellationChangeToken(_cacheCts.Token));

        _cache.Set(cacheKey, contactDto, cacheEntryOptions);

        return contactDto;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ContactMessageDto>> GetContactMessagesAsync(
        int page = 1,
        int pageSize = 20,
        ContactStatus? status = null,
        ContactType? contactType = null,
        string? email = null)
    {
        // Create a cache key based on the parameters
        var cacheKey = $"contact_messages_page{page}_size{pageSize}";
        if (status.HasValue)
            cacheKey += $"_status{status.Value}";
        if (contactType.HasValue)
            cacheKey += $"_type{contactType.Value}";
        if (!string.IsNullOrWhiteSpace(email))
            cacheKey += $"_email{email}";

        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out IEnumerable<ContactMessageDto>? cachedContacts))
        {
            return cachedContacts ?? Enumerable.Empty<ContactMessageDto>();
        }

        var query = _context.ContactMessages
            .Include(c => c.Files)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        if (contactType.HasValue)
            query = query.Where(c => c.ContactType == contactType.Value);

        // T041: Email filtering
        if (!string.IsNullOrWhiteSpace(email))
            query = query.Where(c => c.Email.ToLower() == email.ToLower());

        // T045: Default ORDER BY CreatedAt DESC (already implemented)
        var contacts = await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id) // Secondary sort by ID for deterministic results when CreatedAt is the same
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = contacts.Select(MapToDto).ToList();

        // Cache the result with cancellation token for invalidation
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(CacheExpiry)
            .AddExpirationToken(new CancellationChangeToken(_cacheCts.Token));
        
        _cache.Set(cacheKey, result, cacheEntryOptions);

        return result;
    }
    /// <inheritdoc/>
    public async Task<ContactMessageDto> UpdateContactStatusAsync(int id, UpdateContactStatusRequest request)
    {
        var contact = await _context.ContactMessages.FindAsync(id);
        if (contact == null)
            throw new Maliev.ContactService.Api.Exceptions.NotFoundException($"Contact message with id {id} not found");

        // T042: Check for concurrent updates (UpdatedAt within 5 minutes)
        var fiveMinutesAgo = DateTimeOffset.UtcNow.AddMinutes(-5);
        if (contact.UpdatedAt > fiveMinutesAgo)
        {
            _logger.LogWarning(
                "Potential concurrent update detected for contact {ContactId}. Last update was at {UpdatedAt}, only {Minutes} minutes ago.",
                id,
                contact.UpdatedAt,
                (DateTimeOffset.UtcNow - contact.UpdatedAt).TotalMinutes);
        }

        contact.Status = request.Status;
        if (request.Priority.HasValue)
            contact.Priority = request.Priority.Value;

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
            // T043: Handle concurrency exception with 409 Conflict
            _logger.LogError(ex, "Concurrency conflict detected while updating contact {ContactId}", id);
            throw new InvalidOperationException(
                $"The contact message was modified by another user. Please refresh and try again.", ex);
        }

        // Invalidate all cache entries
        InvalidateAllCache();

        _logger.LogInformation("Updated contact message {ContactId} status to {Status}", id, request.Status);

        return await GetContactMessageByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve updated contact message");
    }
    /// <inheritdoc/>
    public async Task DeleteContactMessageAsync(int id)
    {
        var contact = await _context.ContactMessages.FindAsync(id);
        if (contact == null) 
            throw new Maliev.ContactService.Api.Exceptions.NotFoundException($"Contact message with id {id} not found");

        _context.ContactMessages.Remove(contact);
        await _context.SaveChangesAsync();

        // Invalidate all cache entries
        InvalidateAllCache();

        _logger.LogInformation("Deleted contact message {ContactId}", id);
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public async Task<ContactFileDto?> GetContactFileByIdAsync(int contactId, int fileId)
    {
        var file = await _context.ContactFiles
            .Where(f => f.ContactMessageId == contactId && f.Id == fileId)
            .Select(f => new ContactFileDto
            {
                Id = f.Id,
                FileName = f.FileName,
                ObjectName = f.ObjectName,
                FileSize = f.FileSize,
                ContentType = f.ContentType,
                UploadServiceFileId = f.UploadServiceFileId,
                CreatedAt = f.CreatedAt
            })
            .FirstOrDefaultAsync();

        return file;
    }
    /// <inheritdoc/>
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

        // Invalidate all cache entries
        InvalidateAllCache();

        _logger.LogInformation("Deleted contact file {FileId} from contact {ContactId}", fileId, contactId);
    }

    private static void InvalidateAllCache()
    {
        _cacheCts.Cancel();
        _cacheCts.Dispose();
        _cacheCts = new CancellationTokenSource();
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
            CountryId = contact.CountryId,
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
