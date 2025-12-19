using Asp.Versioning;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Maliev.ContactService.Api.Controllers;

/// <summary>
/// API controller for managing contact messages.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("contact/v{version:apiVersion}/contacts")]
[Produces("application/json")]
public class ContactsController : ControllerBase
{
    private readonly IContactService _contactService;
    private readonly IUploadServiceClient _uploadService;
    private readonly ILogger<ContactsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactsController"/> class.
    /// </summary>
    /// <param name="contactService">The contact service.</param>
    /// <param name="uploadService">The upload service client.</param>
    /// <param name="logger">The logger instance.</param>
    public ContactsController(IContactService contactService, IUploadServiceClient uploadService, ILogger<ContactsController> logger)
    {
        _contactService = contactService;
        _uploadService = uploadService;
        _logger = logger;
    }

    /// <summary>
    /// Submit a contact form message
    /// </summary>
    /// <param name="request">The contact form data</param>
    /// <returns>The created contact message</returns>
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("ContactPolicy")]
    public async Task<ActionResult<ContactMessageDto>> CreateContactMessage(CreateContactMessageRequest request)
    {
        // Let ExceptionHandlingMiddleware handle exceptions for proper status codes
        // (409 for DuplicateInquiryException, 503 for CountryServiceException, etc.)
        var contact = await _contactService.CreateContactMessageAsync(request);
        return CreatedAtAction(nameof(GetContactMessage), new { id = contact.Id }, contact);
    }

    /// <summary>
    /// Get all contact messages (admin only)
    /// </summary>
    /// <param name="pagination">Pagination parameters</param>
    /// <param name="status">Filter by status</param>
    /// <param name="contactType">Filter by contact type</param>
    /// <param name="email">Filter by email address</param>
    /// <returns>List of contact messages</returns>
    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    [EnableRateLimiting("GlobalPolicy")]
    public async Task<ActionResult<IEnumerable<ContactMessageDto>>> GetContactMessages(
        [FromQuery] PaginationParameters pagination,
        [FromQuery] ContactStatus? status = null,
        [FromQuery] ContactType? contactType = null,
        [FromQuery] string? email = null)
    {
        try
        {
            // T044: Validate pagination parameters
            if (pagination.PageSize < 1 || pagination.PageSize > 100)
            {
                return BadRequest(new { message = "PageSize must be between 1 and 100" });
            }

            if (pagination.Page < 1)
            {
                return BadRequest(new { message = "Page must be greater than or equal to 1" });
            }

            var contacts = await _contactService.GetContactMessagesAsync(pagination.Page, pagination.PageSize, status, contactType, email);
            return Ok(contacts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contact messages with pagination Page={Page}, PageSize={PageSize}, Status={Status}, ContactType={ContactType}, Email={Email}",
                pagination.Page, pagination.PageSize, status, contactType, email);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get a specific contact message (admin only)
    /// </summary>
    /// <param name="id">Contact message ID</param>
    /// <returns>The contact message</returns>
    [HttpGet("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [EnableRateLimiting("GlobalPolicy")]
    public async Task<ActionResult<ContactMessageDto>> GetContactMessage(int id)
    {
        try
        {
            var contact = await _contactService.GetContactMessageByIdAsync(id);
            if (contact == null)
            {
                return NotFound();
            }

            return Ok(contact);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contact message with id {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Update contact message status (admin only)
    /// </summary>
    /// <param name="id">Contact message ID</param>
    /// <param name="request">Status update request</param>
    /// <returns>Updated contact message</returns>
    [HttpPut("{id}/status")]
    [Authorize(Policy = "AdminOnly")]
    [EnableRateLimiting("GlobalPolicy")]
    public async Task<ActionResult<ContactMessageDto>> UpdateContactStatus(int id, UpdateContactStatusRequest request)
    {
        try
        {
            var contact = await _contactService.UpdateContactStatusAsync(id, request);
            return Ok(contact);
        }
        catch (Maliev.ContactService.Api.Exceptions.NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Delete a contact message (admin only)
    /// </summary>
    /// <param name="id">Contact message ID</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [EnableRateLimiting("GlobalPolicy")]
    public async Task<IActionResult> DeleteContactMessage(int id)
    {
        try
        {
            await _contactService.DeleteContactMessageAsync(id);
            return NoContent();
        }
        catch (Maliev.ContactService.Api.Exceptions.NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Get files for a contact message (admin only)
    /// </summary>
    /// <param name="id">Contact message ID</param>
    /// <returns>List of files</returns>
    [HttpGet("{id}/files")]
    [Authorize(Policy = "AdminOnly")]
    [EnableRateLimiting("GlobalPolicy")]
    public async Task<ActionResult<IEnumerable<ContactFileDto>>> GetContactFiles(int id)
    {
        try
        {
            var files = await _contactService.GetContactFilesAsync(id);
            return Ok(files);
        }
        catch (Maliev.ContactService.Api.Exceptions.NotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contact files for contact message with id {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Delete a file from a contact message (admin only)
    /// </summary>
    /// <param name="id">Contact message ID</param>
    /// <param name="fileId">File ID</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{id}/files/{fileId}")]
    [Authorize(Policy = "AdminOnly")]
    [EnableRateLimiting("GlobalPolicy")]
    public async Task<IActionResult> DeleteContactFile(int id, int fileId)
    {
        try
        {
            await _contactService.DeleteContactFileAsync(id, fileId);
            return NoContent();
        }
        catch (Maliev.ContactService.Api.Exceptions.NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Download a file from a contact message (admin only)
    /// </summary>
    /// <param name="id">Contact message ID</param>
    /// <param name="fileId">File ID</param>
    /// <returns>The file content</returns>
    [HttpGet("{id}/files/{fileId}/download")]
    [Authorize(Policy = "AdminOnly")]
    [EnableRateLimiting("GlobalPolicy")]
    public async Task<IActionResult> DownloadContactFile(int id, int fileId)
    {
        try
        {
            var file = await _contactService.GetContactFileByIdAsync(id, fileId);

            if (file == null || string.IsNullOrEmpty(file.UploadServiceFileId))
            {
                return NotFound();
            }

            var downloadResponse = await _uploadService.DownloadFileAsync(file.UploadServiceFileId);
            return File(downloadResponse.Content, downloadResponse.ContentType, downloadResponse.FileName);
        }
        catch (Maliev.ContactService.Api.Exceptions.NotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading contact file with id {FileId} for contact message with id {Id}", fileId, id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
