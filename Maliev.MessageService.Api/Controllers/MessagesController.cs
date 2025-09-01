// <copyright file="MessagesController.cs" company="Maliev Company Limited">
// Copyright (c) Maliev Company Limited. All rights reserved.
// </copyright>

namespace Maliev.MessageService.Api.Controllers
{
    using System.Threading.Tasks;
    using Maliev.MessageService.Api.Models.DTOs;
    using Maliev.MessageService.Api.Services;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// Controller.
    /// </summary>
    /// <seealso cref="Microsoft.AspNetCore.Mvc.ControllerBase" />
    [Route("[controller]")]
    [ApiController]
    [ApiConventionType(typeof(DefaultApiConventions))]
    [Authorize(AuthenticationSchemes=JwtBearerDefaults.AuthenticationScheme)]
    public class MessagesController : ControllerBase
    {
        /// <summary>
        /// The message service.
        /// </summary>
        private readonly IMessageService _messageService;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagesController" /> class.
        /// </summary>
        /// <param name="messageService">The message service.</param>
        public MessagesController(IMessageService messageService)
        {
            _messageService = messageService;
        }

        /// <summary>
        /// Create the message.
        /// </summary>
        /// <param name="request">The create message request.</param>
        /// <returns>
        ///   <see cref="ActionResult" />.
        /// </returns>
        [HttpPost]
        public async Task<ActionResult<MessageDto>> CreateMessageAsync([FromBody] CreateMessageRequest request)
        {
            if (request == null)
            {
                return BadRequest("Message is required");
            }

            var messageDto = await _messageService.CreateMessageAsync(request);
            return CreatedAtRoute("GetMessage", new { messageId = messageDto.Id }, messageDto);
        }

        /// <summary>
        /// Delete the message.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <returns>
        ///   <see cref="ActionResult" />.
        /// </returns>
        [HttpDelete("{messageId:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult> DeleteMessageAsync(int messageId)
        {
            try
            {
                await _messageService.DeleteMessageAsync(messageId);
                return NoContent();
            }
            catch (System.Exception)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Gets the message.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <returns>
        ///   <see cref="ActionResult{MessageDto}" />.
        /// </returns>
        [HttpGet("{messageId:int}", Name = "GetMessage")]
        public async Task<ActionResult<MessageDto>> GetMessageAsync(int messageId)
        {
            var messageDto = await _messageService.GetMessageAsync(messageId);
            if (messageDto == null)
            {
                return NotFound();
            }
            return messageDto;
        }

        /// <summary>
        /// Get paginated messages.
        /// </summary>
        /// <param name="sortType">The sort type.</param>
        /// <param name="query">The query string.</param>
        /// <param name="pageNumber">The page number.</param>
        /// <param name="pageSize">The page size.</param>
        /// <returns>
        ///   <see cref="PaginatedListDto{MessageDto}" />.
        /// </returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<PaginatedListDto<MessageDto>>> GetPaginatedAsync(
            [FromQuery] MessageSortType? sortType,
            [FromQuery] string query,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            var paginatedList = await _messageService.GetPaginatedAsync(sortType, query, pageNumber, pageSize);
            if (paginatedList == null || paginatedList.Items.Count == 0)
            {
                return NotFound();
            }
            return paginatedList;
        }

        /// <summary>
        /// Update the message.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="request">The update message request.</param>
        /// <returns>
        ///   <see cref="ActionResult" />.
        /// </returns>
        [HttpPut("{messageId:int}")]
        public async Task<ActionResult> UpdateMessageAsync(int messageId, [FromBody] UpdateMessageRequest request)
        {
            if (request == null)
            {
                return BadRequest("Message is required");
            }

            try
            {
                await _messageService.UpdateMessageAsync(messageId, request);
                return NoContent();
            }
            catch (System.Exception)
            {
                return NotFound();
            }
        }
    }
}