namespace Maliev.MessageService.Api.Services
{
    using System.Threading.Tasks;
    using Maliev.MessageService.Api.Models.DTOs;

    public interface IMessageService
    {
        Task<MessageDto> CreateMessageAsync(CreateMessageRequest request);
        Task DeleteMessageAsync(int id);
        Task<MessageDto> GetMessageAsync(int id);
        Task<PaginatedListDto<MessageDto>> GetPaginatedAsync(MessageSortType? sortType, string query, int? pageNumber, int? pageSize);
        Task UpdateMessageAsync(int id, UpdateMessageRequest request);
    }
}