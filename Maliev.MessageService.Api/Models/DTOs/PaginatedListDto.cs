namespace Maliev.MessageService.Api.Models.DTOs
{
    using System.Collections.Generic;

    public class PaginatedListDto<T>
    {
        public PaginatedListDto(List<T> items, int count, int pageIndex, int pageSize)
        {
            PageIndex = pageIndex;
            TotalPages = (int)Math.Ceiling(count / (double)pageSize);
            TotalRecords = count;
            Items = items;
        }

        public int PageIndex { get; private set; }

        public int TotalPages { get; private set; }

        public int TotalRecords { get; private set; }

        public List<T> Items { get; private set; }

        public bool HasPreviousPage
        {
            get
            {
                return (PageIndex > 1);
            }
        }

        public bool HasNextPage
        {
            get
            {
                return (PageIndex < TotalPages);
            }
        }
    }
}