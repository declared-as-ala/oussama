using System.Collections.Generic;

namespace DocApi.DTOs.CorrectiveActions
{
    public class PagedCorrectiveActionResponse
    {
        public int Total { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public List<CorrectiveActionListItemResponse> Items { get; set; } = new();
    }
}
