using System.Collections.Generic;

namespace DocApi.DTOs.Processes
{
    public class PagedProcessResponse
    {
        public int Total { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public List<ProcessListItemResponse> Items { get; set; } = new();
    }
}
