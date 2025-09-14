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
    /// <param name="page">Page number</param>
    /// <param name="pageSize">Items per page</param>
    /// <param name="status">Filter by status</param>
    /// <param name="contactType">Filter by contact type</param>
    /// <returns>List of contact messages</returns>
    [HttpGet]
    [Authorize]
    [EnableRateLimiting("GlobalPolicy")]
    public async Task<ActionResult<IEnumerable<ContactMessageDto>>> GetContactMessages(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ContactStatus? status = null,
        [FromQuery] ContactType? contactType = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var contacts = await _contactService.GetContactMessagesAsync(page, pageSize, status, contactType);
        return Ok(contacts);
    }

    /// <summary>
    /// Get a specific contact message (admin only)
    /// </summary>
    /// <param name="id">Contact message ID</param>
    /// <returns>The contact message</returns>
    [HttpGet("{id}")]
    [Authorize]
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
    [Authorize]
    [EnableRateLimiting("GlobalPolicy")]
    public async Task<ActionResult<ContactMessageDto>> UpdateContactStatus(int id, UpdateContactStatusRequest request)
    {
        var contact = await _contactService.UpdateContactStatusAsync(id, request);
        if (contact == null)
        {
            return NotFound();
        }

        return Ok(contact);
    }

    /// <summary>
    /// Delete a contact message (admin only)
    /// </summary>
    /// <param name="id">Contact message ID</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{id}")]
    [Authorize]
    [EnableRateLimiting("GlobalPolicy")]
    public async Task<IActionResult> DeleteContactMessage(int id)
    {
        var deleted = await _contactService.DeleteContactMessageAsync(id);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    /// Get files for a contact message (admin only)
    /// </summary>
    /// <param name="id">Contact message ID</param>
    /// <returns>List of files</returns>
    [HttpGet("{id}/files")]
    [Authorize]
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
    [Authorize]
    [EnableRateLimiting("GlobalPolicy")]
    public async Task<IActionResult> DeleteContactFile(int id, int fileId)
    {
        var deleted = await _contactService.DeleteContactFileAsync(id, fileId);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    /// Download a file from a contact message (admin only)
    /// </summary>
    /// <param name="id">Contact message ID</param>
    /// <param name="fileId">File ID</param>
    /// <returns>The file content</returns>
    [HttpGet("{id}/files/{fileId}/download")]
    [Authorize]
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