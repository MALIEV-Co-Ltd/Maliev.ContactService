using Asp.Versioning;
using Maliev.ContactService.Api.Models;
using Maliev.ContactService.Api.Services;
using Maliev.ContactService.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Maliev.ContactService.Api.Controllers;

[ApiController]
[Route("v{version:apiVersion}/contacts")]
[ApiVersion("1.0")]
public class ContactsController : ControllerBase
{
    private readonly IContactService _contactService;
    private readonly IUploadServiceClient _uploadService;
    private readonly ILogger<ContactsController> _logger;

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
        var contact = await _contactService.CreateContactMessageAsync(request);
        return CreatedAtAction(nameof(GetContactMessage), new { id = contact.Id }, contact);
    }

    /// <summary>
    /// Get all contact messages (admin only)
    /// </summary>
    /// <param name="pagination">Pagination parameters</param>
    /// <param name="status">Filter by status</param>
    /// <param name="contactType">Filter by contact type</param>
    /// <returns>List of contact messages</returns>
    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    [EnableRateLimiting("GlobalPolicy")]
    public async Task<ActionResult<IEnumerable<ContactMessageDto>>> GetContactMessages(
        [FromQuery] PaginationParameters pagination,
        [FromQuery] ContactStatus? status = null,
        [FromQuery] ContactType? contactType = null)
    {
        var contacts = await _contactService.GetContactMessagesAsync(pagination.Page, pagination.PageSize, status, contactType);
        return Ok(contacts);
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
        var contact = await _contactService.GetContactMessageByIdAsync(id);
        if (contact == null)
        {
            return NotFound();
        }

        return Ok(contact);
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
        var files = await _contactService.GetContactFilesAsync(id);
        return Ok(files);
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
        var files = await _contactService.GetContactFilesAsync(id);
        var file = files.FirstOrDefault(f => f.Id == fileId);

        if (file == null || string.IsNullOrEmpty(file.UploadServiceFileId))
        {
            return NotFound();
        }

        var downloadResponse = await _uploadService.DownloadFileAsync(file.UploadServiceFileId);
        return File(downloadResponse.Content, downloadResponse.ContentType, downloadResponse.FileName);
    }
}