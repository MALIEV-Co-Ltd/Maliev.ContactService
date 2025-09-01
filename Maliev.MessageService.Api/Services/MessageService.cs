namespace Maliev.MessageService.Api.Services
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Maliev.MessageService.Api.Models.DTOs;
    using Maliev.MessageService.Data.Data;
    using Maliev.MessageService.Data.Models;
    using Microsoft.EntityFrameworkCore;

    public class MessageService : IMessageService
    {
        private readonly MessageContext _context;

        public MessageService(MessageContext context)
        {
            _context = context;
        }

        public async Task<MessageDto> CreateMessageAsync(CreateMessageRequest request)
        {
            var message = new Message
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Company = request.Company,
                Email = request.Email,
                Telephone = request.Telephone,
                Country = request.Country,
                MessageContent = request.MessageContent,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow
            };

            _context.Message.Add(message);
            await _context.SaveChangesAsync();

            return new MessageDto
            {
                Id = message.Id,
                FirstName = message.FirstName,
                LastName = message.LastName,
                Company = message.Company,
                Email = message.Email,
                Telephone = message.Telephone,
                Country = message.Country,
                MessageContent = message.MessageContent,
                CreatedDate = message.CreatedDate,
                ModifiedDate = message.ModifiedDate
            };
        }

        public async Task DeleteMessageAsync(int id)
        {
            var message = await _context.Message.FindAsync(id);
            if (message == null)
            {
                throw new Exception("Message not found"); // Or a custom NotFoundException
            }

            _context.Message.Remove(message);
            await _context.SaveChangesAsync();
        }

        public async Task<MessageDto> GetMessageAsync(int id)
        {
            var message = await _context.Message.FindAsync(id);
            if (message == null)
            {
                return null;
            }

            return new MessageDto
            {
                Id = message.Id,
                FirstName = message.FirstName,
                LastName = message.LastName,
                Company = message.Company,
                Email = message.Email,
                Telephone = message.Telephone,
                Country = message.Country,
                MessageContent = message.MessageContent,
                CreatedDate = message.CreatedDate,
                ModifiedDate = message.ModifiedDate
            };
        }

        public async Task<PaginatedListDto<MessageDto>> GetPaginatedAsync(MessageSortType? sortType, string query, int? pageNumber, int? pageSize)
        {
            var messages = _context.Message.AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                messages = messages.Where(m =>
                    m.FirstName.Contains(query) ||
                    m.LastName.Contains(query) ||
                    m.Company.Contains(query) ||
                    m.Email.Contains(query) ||
                    m.Telephone.Contains(query) ||
                    m.Country.Contains(query) ||
                    m.MessageContent.Contains(query) ||
                    m.Id.ToString().Contains(query));
            }

            messages = sortType switch
            {
                MessageSortType.MessageId_Ascending => messages.OrderBy(m => m.Id),
                MessageSortType.MessageId_Descending => messages.OrderByDescending(m => m.Id),
                MessageSortType.MessageCreatedDate_Ascending => messages.OrderBy(m => m.CreatedDate),
                MessageSortType.MessageCreatedDate_Descending => messages.OrderByDescending(m => m.CreatedDate),
                _ => messages.OrderBy(m => m.Id),
            };

            var count = await messages.CountAsync();
            var items = await messages.Skip(((pageNumber ?? 1) - 1) * (pageSize ?? 10)).Take(pageSize ?? 10).ToListAsync();

            var messageDtos = items.Select(message => new MessageDto
            {
                Id = message.Id,
                FirstName = message.FirstName,
                LastName = message.LastName,
                Company = message.Company,
                Email = message.Email,
                Telephone = message.Telephone,
                Country = message.Country,
                MessageContent = message.MessageContent,
                CreatedDate = message.CreatedDate,
                ModifiedDate = message.ModifiedDate
            }).ToList();

            return new PaginatedListDto<MessageDto>(messageDtos, count, pageNumber ?? 1, pageSize ?? 10);
        }

        public async Task UpdateMessageAsync(int id, UpdateMessageRequest request)
        {
            var message = await _context.Message.FindAsync(id);
            if (message == null)
            {
                throw new Exception("Message not found"); // Or a custom NotFoundException
            }

            message.FirstName = request.FirstName;
            message.LastName = request.LastName;
            message.Company = request.Company;
            message.Email = request.Email;
            message.Telephone = request.Telephone;
            message.Country = request.Country;
            message.MessageContent = request.MessageContent;
            message.ModifiedDate = DateTime.UtcNow;

            _context.Entry(message).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }
    }
}